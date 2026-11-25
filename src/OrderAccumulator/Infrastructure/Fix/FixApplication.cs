using Microsoft.Extensions.Logging;
using OrderProcessing.Domain;
using QuickFix;
using QuickFix.Fields;
using FixNewOrderSingle = QuickFix.FIX44.NewOrderSingle;
using FixExecutionReport = QuickFix.FIX44.ExecutionReport;

namespace OrderAccumulator.Infrastructure.Fix;

public sealed class FixApplication(OrderProcessor processor, ILogger<FixApplication> logger)
    : MessageCracker, IApplication
{
    public void OnCreate(SessionID sessionID) =>
        logger.LogInformation("FIX session created {SessionId}", sessionID);

    public void OnLogon(SessionID sessionID) =>
        logger.LogInformation("FIX session logged on {SessionId}", sessionID);

    public void OnLogout(SessionID sessionID) =>
        logger.LogWarning("FIX session logged out or disconnected {SessionId}", sessionID);

    public void ToAdmin(Message message, SessionID sessionID) { }
    public void FromAdmin(Message message, SessionID sessionID) { }
    public void ToApp(Message message, SessionID sessionID) { }

    public void FromApp(Message message, SessionID sessionID) => Crack(message, sessionID);

    public void OnMessage(FixNewOrderSingle message, SessionID sessionID)
    {
        string clOrdId = message.GetString(Tags.ClOrdID);
        string symbol = message.GetString(Tags.Symbol);
        string side = message.GetString(Tags.Side);
        string type = message.GetString(Tags.OrdType);
        _ = message.GetString(Tags.TransactTime);
        if (type == "2")
        {
            // QuickFIX/n turns a missing conditional field into a Business Message Reject.
            _ = message.GetString(Tags.Price);
        }

        ExactNumber quantity = ParseNumber(message.GetString(Tags.OrderQty), Tags.OrderQty, sessionID);
        ExactNumber? price = message.IsSetField(Tags.Price)
            ? ParseNumber(message.GetString(Tags.Price), Tags.Price, sessionID)
            : null;

        // The FIX dictionary validates the CHAR representation and enum before dispatch.
        OrderSide orderSide = side switch
        {
            "1" => OrderSide.Buy,
            "2" => OrderSide.Sell,
            _ => (OrderSide)(1000 + side[0])
        };
        OrderType orderType = type == "2" ? OrderType.Limit : OrderType.Unsupported(type);
        var order = OrderRequest.FromExactNumbers(clOrdId, symbol, orderSide, quantity, price, orderType);
        logger.LogInformation("FIX NewOrderSingle received {SessionId} {ClOrdId}", sessionID, SafeId(clOrdId));

        OrderProcessingResult result;
        try
        {
            result = processor.Process(order);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Order processing failed; outcome may be unknown {SessionId} {ClOrdId}",
                sessionID, SafeId(clOrdId));
            throw;
        }

        string? execId = null;
        try
        {
            FixExecutionReport report = CreateReport(result);
            execId = report.GetString(Tags.ExecID);
            if (!Session.SendToTarget(report, sessionID))
            {
                logger.LogError("FIX report not sent; outcome is unknown to peer {SessionId} {ClOrdId} {OrderId} {ExecId}",
                    sessionID, SafeId(clOrdId), result.OrderId, execId);
                return;
            }

            logger.LogInformation("FIX report sent {SessionId} {ClOrdId} {OrderId} {ExecId} {Status}",
                sessionID, SafeId(clOrdId), result.OrderId, execId, result.Status);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "FIX report creation or send failed; outcome is unknown to peer {SessionId} {ClOrdId} {OrderId} {ExecId}",
                sessionID, SafeId(clOrdId), result.OrderId, execId);
        }
    }

    private ExactNumber ParseNumber(string raw, int tag, SessionID sessionId)
    {
        if (ExactNumber.TryParse(raw, out ExactNumber? value, out NumericInputError error))
        {
            return value;
        }

        logger.LogWarning("FIX numeric field invalid {SessionId} {Tag} {Error}", sessionId, tag, error);
        throw new IncorrectDataFormat(tag, new FormatException("Invalid FIX numeric field."));
    }

    private static FixExecutionReport CreateReport(OrderProcessingResult result)
    {
        OrderRequest order = result.Order;
        bool accepted = result.Status == OrderProcessingStatus.Accepted;
        bool quantityRepresentable = order.ExactQuantity.TryToDecimal(out decimal quantity);
        decimal price = default;
        bool priceRepresentable = order.ExactPrice is null || order.ExactPrice.TryToDecimal(out price);
        if (accepted && (!quantityRepresentable || order.ExactPrice is null || !priceRepresentable))
        {
            throw new InvalidOperationException("Accepted order numbers must be exactly representable.");
        }

        string side = order.Side switch
        {
            OrderSide.Buy => "1",
            OrderSide.Sell => "2",
            _ => ((char)((int)order.Side - 1000)).ToString()
        };

        var report = new FixExecutionReport();
        report.SetField(new OrderID(result.OrderId));
        report.SetField(new ExecID(Guid.NewGuid().ToString("N")));
        report.SetField(new ExecType(accepted ? ExecType.NEW : ExecType.REJECTED));
        report.SetField(new OrdStatus(accepted ? OrdStatus.NEW : OrdStatus.REJECTED));
        report.SetField(new ClOrdID(order.ClOrdId!));
        report.SetField(new Symbol(order.Symbol!));
        report.SetField(new Side(side[0]));
        if (quantityRepresentable)
        {
            report.SetField(new OrderQty(quantity));
        }

        if (order.ExactPrice is not null && priceRepresentable)
        {
            report.SetField(new Price(price));
        }

        report.SetField(new LeavesQty(accepted ? quantity : 0m));
        report.SetField(new CumQty(0m));
        report.SetField(new AvgPx(0m));
        report.SetField(new TransactTime(DateTime.UtcNow));
        if (!accepted)
        {
            int reason = result.RejectionReason switch
            {
                OrderRejectionReason.UnsupportedSymbol => 1,
                OrderRejectionReason.ExposureLimitExceeded => 3,
                OrderRejectionReason.IdentifierConflict => 6,
                OrderRejectionReason.UnsupportedOrderType => 11,
                OrderRejectionReason.InvalidQuantity => 13,
                _ => 0
            };
            report.SetField(new OrdRejReason(reason));
            string text = result.RejectionText ?? "Order rejected.";
            if (!quantityRepresentable)
            {
                text += " OrderQty omitted: value cannot be represented exactly as decimal.";
            }

            if (!priceRepresentable)
            {
                text += " Price omitted: value cannot be represented exactly as decimal.";
            }

            report.SetField(new Text(text));
        }

        return report;
    }

    private static string SafeId(string value) =>
        new(value.Take(128).Select(character => char.IsControl(character) ? '?' : character).ToArray());
}
