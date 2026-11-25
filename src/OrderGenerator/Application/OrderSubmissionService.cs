using Microsoft.Extensions.Options;
using OrderProcessing.Domain;

namespace OrderGenerator.Application;

public sealed class OrderSubmissionService(
    IOrderTransport transport,
    PendingOrderRegistry registry,
    IOptions<OrderSubmissionOptions> options,
    ILogger<OrderSubmissionService> logger) : IOrderSubmissionService
{
    public async Task<SubmissionResult> SubmitAsync(OrderSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var errors = new List<string>();
        if (!SupportedSymbols.IsSupported(submission.Symbol)) errors.Add("Unsupported symbol.");
        if (submission.Side is not (OrderSide.Buy or OrderSide.Sell)) errors.Add("Side must be Buy or Sell.");
        if (submission.Quantity is < 1 or >= 100000) errors.Add("Quantity must be an integer from 1 to 99999.");
        if (submission.Price <= 0m || submission.Price >= 1000m || submission.Price % 0.01m != 0m)
            errors.Add("Price must be positive, below 1000 and a multiple of 0.01.");
        if (errors.Count != 0)
            return new(SubmissionStatus.ValidationFailed, null, "Invalid order.", ValidationErrors: errors);

        string id = Guid.NewGuid().ToString("N");
        if (cancellationToken.IsCancellationRequested)
            return new(SubmissionStatus.Cancelled, id, "Submission cancelled before sending.");

        var order = new OutboundOrder(id, submission.Symbol!, submission.Side, submission.Quantity, submission.Price);
        if (!registry.TryRegister(order, options.Value.MaxPendingOrders, out var pending, out string error))
            return new(SubmissionStatus.Unavailable, id, error);

        var registered = pending!;
        // Register cancellation and the deadline before starting the send, so they
        // participate in the same atomic completion race as logout and reports.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.ResponseTimeoutSeconds));
        using var timeoutRegistration = timeout.Token.Register(() =>
            registry.CompleteInterrupted(registered, SubmissionStatus.TimedOut,
                "Response timed out; do not resubmit automatically. Reconcile the original identifier."));
        using var cancellationRegistration = cancellationToken.Register(() =>
            registry.CompleteInterrupted(registered, SubmissionStatus.Cancelled,
                "Submission cancelled; reconcile the original identifier if its outcome is unknown."));

        if (registry.TryBeginSend(registered))
        {
            try
            {
                if (!transport.Send(order))
                    registry.CompleteInterrupted(registered, SubmissionStatus.SendFailed,
                        "FIX send returned false; the order outcome may be unknown.");
            }
            catch (Exception exception)
            {
                logger.LogError("FIX send failed for {ClOrdId}; error type {ErrorType}; outcome may be unknown",
                    id, exception.GetType().Name);
                registry.CompleteInterrupted(registered, SubmissionStatus.SendFailed,
                    "FIX send failed; the order outcome may be unknown.");
            }
        }

        SubmissionResult result = await registered.Completion.Task;
        if (result.Status == SubmissionStatus.TimedOut)
            logger.LogWarning("FIX response timeout for {ClOrdId}", id);
        return result;
    }
}
