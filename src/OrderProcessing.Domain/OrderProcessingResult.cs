namespace OrderProcessing.Domain;

/// <summary>A conflict is a business rejection of identifier reuse, not a new original result.</summary>
public enum OrderProcessingStatus
{
    Accepted,
    Rejected,
    Conflict
}

public enum OrderRejectionReason
{
    InvalidClOrdId,
    UnsupportedSymbol,
    InvalidSide,
    InvalidQuantity,
    InvalidPrice,
    ExposureLimitExceeded,
    IdentifierConflict,
    UnsupportedOrderType
}

/// <summary>
/// A business decision, not a market execution. Technical failures propagate as exceptions.
/// </summary>
public sealed class OrderProcessingResult
{
    private OrderProcessingResult(
        OrderRequest order,
        OrderProcessingStatus status,
        OrderRejectionReason? rejectionReason,
        string? rejectionText)
    {
        OrderId = Guid.NewGuid().ToString("N");
        Order = order;
        Status = status;
        RejectionReason = rejectionReason;
        RejectionText = rejectionText;
    }

    /// <summary>
    /// Server-generated identifier. Original decisions with usable client identifiers
    /// retain this value on replay; conflicts and missing-identifier results are not stored.
    /// </summary>
    public string OrderId { get; }
    public OrderRequest Order { get; }
    public OrderProcessingStatus Status { get; }
    public OrderRejectionReason? RejectionReason { get; }
    public string? RejectionText { get; }

    internal static OrderProcessingResult Accept(OrderRequest order) =>
        new(order, OrderProcessingStatus.Accepted, null, null);

    internal static OrderProcessingResult Reject(OrderRequest order, OrderRejectionReason reason)
    {
        string text = reason switch
        {
            OrderRejectionReason.InvalidClOrdId => "Client order identifier is required.",
            OrderRejectionReason.UnsupportedSymbol => "Symbol is not supported.",
            OrderRejectionReason.InvalidSide => "Side must be Buy or Sell.",
            OrderRejectionReason.InvalidQuantity => "Quantity must be an integer from 1 through 99999.",
            OrderRejectionReason.InvalidPrice => "Price is required and must be positive, below 1000, and a multiple of 0.01.",
            OrderRejectionReason.ExposureLimitExceeded => "The projected exposure exceeds the limit.",
            OrderRejectionReason.IdentifierConflict => "Client order identifier was already used for different content.",
            OrderRejectionReason.UnsupportedOrderType => "Only Limit orders are supported.",
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };

        OrderProcessingStatus status = reason == OrderRejectionReason.IdentifierConflict
            ? OrderProcessingStatus.Conflict
            : OrderProcessingStatus.Rejected;
        return new OrderProcessingResult(order, status, reason, text);
    }
}
