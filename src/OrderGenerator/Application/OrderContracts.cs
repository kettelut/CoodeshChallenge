using OrderProcessing.Domain;

namespace OrderGenerator.Application;

// Quantity is integral at the API boundary; malformed/fractional form input must
// also be rejected by the future Razor Page's model binding.
public sealed record OrderSubmission(string? Symbol, OrderSide Side, int Quantity, decimal Price);

public enum SubmissionStatus
{
    Accepted,
    Rejected,
    ValidationFailed,
    Unavailable,
    Disconnected,
    SendFailed,
    TimedOut,
    Cancelled,
    TechnicalFailure
}

public sealed record SubmissionResult(
    SubmissionStatus Status,
    string? ClOrdId,
    string Explanation,
    bool OutcomeMayBeUnknown = false,
    string? OrderId = null,
    string? ExecId = null,
    int? RejectionReason = null,
    IReadOnlyList<string>? ValidationErrors = null);

public interface IOrderSubmissionService
{
    Task<SubmissionResult> SubmitAsync(OrderSubmission submission, CancellationToken cancellationToken = default);
}

public sealed record OutboundOrder(string ClOrdId, string Symbol, OrderSide Side, int Quantity, decimal Price);

// Implementations report send failures via exceptions or a false result, never as business rejections.
public interface IOrderTransport
{
    bool Send(OutboundOrder order);
}
