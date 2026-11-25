using OrderGenerator.Application;
using OrderProcessing.Domain;
using QuickFix;
using QuickFix.Fields;

namespace OrderGenerator.Infrastructure.Fix;

public sealed class FixApplication(PendingOrderRegistry registry, ILogger<FixApplication> logger)
    : MessageCracker, IApplication
{
    private SessionID? _sessionId;

    internal void ConfigureSession(SessionID sessionId) => _sessionId = sessionId;

    public void OnCreate(SessionID sessionID) => logger.LogInformation("FIX session created {SessionId}", sessionID);
    public void OnLogon(SessionID sessionID)
    {
        if (!sessionID.Equals(_sessionId)) return;
        registry.Connected();
        logger.LogInformation("FIX session logged on {SessionId}", sessionID);
    }
    public void OnLogout(SessionID sessionID)
    {
        if (!sessionID.Equals(_sessionId)) return;
        registry.Disconnected();
        logger.LogWarning("FIX session logged out or disconnected {SessionId}", sessionID);
    }
    public void ToAdmin(Message message, SessionID sessionID) { }
    public void FromAdmin(Message message, SessionID sessionID) { }
    public void ToApp(Message message, SessionID sessionID) { }
    public void FromApp(Message message, SessionID sessionID) => Crack(message, sessionID);

    public void OnMessage(QuickFix.FIX44.ExecutionReport message, SessionID sessionID)
    {
        if (!sessionID.Equals(_sessionId)) return;
        if (!message.IsSetField(Tags.ClOrdID))
        {
            logger.LogWarning("FIX ExecutionReport without ClOrdID {SessionId}", sessionID);
            return;
        }
        string id = message.GetString(Tags.ClOrdID);
        if (!registry.TryGet(id, out var pending))
        {
            // Do not log untrusted identifiers or payloads from unsolicited messages.
            logger.LogWarning("Unexpected, duplicate or late FIX ExecutionReport {SessionId}", sessionID);
            return;
        }

        SubmissionResult result;
        try
        {
            OutboundOrder order = pending!.Order;
            char execType = message.GetChar(Tags.ExecType);
            char status = message.GetChar(Tags.OrdStatus);
            string orderId = message.GetString(Tags.OrderID);
            string execId = message.GetString(Tags.ExecID);
            char side = message.GetChar(Tags.Side);
            bool consistent = !string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(execId)
                && message.GetString(Tags.Symbol) == order.Symbol
                && side == (order.Side == OrderSide.Buy ? Side.BUY : Side.SELL)
                && ExactEquals(message, Tags.OrderQty, order.Quantity)
                && ExactEquals(message, Tags.Price, order.Price)
                && !string.IsNullOrWhiteSpace(message.GetString(Tags.TransactTime));

            if (consistent && execType == ExecType.NEW && status == OrdStatus.NEW
                && ExactEquals(message, Tags.CumQty, 0m)
                && ExactEquals(message, Tags.LeavesQty, order.Quantity)
                && ExactEquals(message, Tags.AvgPx, 0m))
            {
                result = new(SubmissionStatus.Accepted, id, "Order acknowledged as New; no execution is implied.",
                    OrderId: orderId, ExecId: execId);
            }
            else if (consistent && execType == ExecType.REJECTED && status == OrdStatus.REJECTED
                && ExactEquals(message, Tags.CumQty, 0m)
                && ExactEquals(message, Tags.LeavesQty, 0m)
                && ExactEquals(message, Tags.AvgPx, 0m)
                && message.IsSetField(Tags.OrdRejReason) && message.IsSetField(Tags.Text)
                && !string.IsNullOrWhiteSpace(message.GetString(Tags.Text)))
            {
                result = new(SubmissionStatus.Rejected, id, message.GetString(Tags.Text),
                    OrderId: orderId, ExecId: execId, RejectionReason: message.GetInt(Tags.OrdRejReason));
            }
            else
            {
                result = new(SubmissionStatus.TechnicalFailure, id,
                    "Invalid or unsupported ExecutionReport; reconcile the original order.", true);
            }
        }
        catch (Exception exception) when (exception is FieldNotFoundException or FieldConvertError
            or FormatException or OverflowException or ArgumentException)
        {
            result = new(SubmissionStatus.TechnicalFailure, id,
                "Malformed ExecutionReport; reconcile the original order.", true);
        }
        if (registry.Complete(pending!, result))
            logger.LogInformation("FIX ExecutionReport correlated {SessionId} {ClOrdId} {Status}", sessionID, id, result.Status);
        else
            logger.LogWarning("Late or duplicate FIX ExecutionReport {SessionId} {ClOrdId}", sessionID, id);
    }

    private static bool ExactEquals(Message message, int tag, decimal expected) =>
        ExactNumber.TryParse(message.GetString(tag), out var actual, out _)
        && actual == ExactNumber.FromDecimal(expected);
}
