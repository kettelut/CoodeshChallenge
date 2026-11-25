using System.Collections.Immutable;

namespace OrderProcessing.Domain;

/// <summary>
/// Processes orders against one in-memory state. Acceptance accounts for full notional
/// immediately, but does not represent a market execution.
/// </summary>
/// <remarks>
/// One lock serializes all symbols and identifiers. Exposure and original decisions are
/// published together in one immutable snapshot. State and replay history are lost on
/// restart and are not shared across instances; history grows for the instance lifetime.
/// </remarks>
public sealed class OrderProcessor
{
    public const decimal MaximumExposure = 100_000_000m;

    private readonly object _gate = new();
    private readonly Action<OrderProcessingStage>? _beforeCommit;
    private Snapshot _state = Snapshot.Empty;
    private bool _isProcessing;

    /// <param name="beforeCommit">
    /// Optional synchronous fault-injection hook. It runs under the global lock and must
    /// not perform I/O or call Process recursively. Exceptions propagate without a commit.
    /// </param>
    public OrderProcessor(Action<OrderProcessingStage>? beforeCommit = null)
    {
        _beforeCommit = beforeCommit;
    }

    /// <summary>
    /// Processes a submission, or returns its original decision on an identical replay.
    /// A null, empty or whitespace-only identifier is rejected without registering history.
    /// </summary>
    public OrderProcessingResult Process(OrderRequest order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrWhiteSpace(order.ClOrdId))
        {
            return OrderProcessingResult.Reject(order, OrderRejectionReason.InvalidClOrdId);
        }

        lock (_gate)
        {
            if (_isProcessing)
            {
                throw new InvalidOperationException("Recursive order processing is not supported.");
            }

            _isProcessing = true;
            try
            {
                return ProcessUnderLock(order);
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>Returns the current exposure for an exactly matched supported symbol.</summary>
    /// <exception cref="ArgumentException">The symbol is not supported.</exception>
    public decimal GetExposure(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (!SupportedSymbols.IsSupported(symbol))
        {
            throw new ArgumentException("Symbol is not supported.", nameof(symbol));
        }

        lock (_gate)
        {
            return _state.Exposures.GetValueOrDefault(symbol);
        }
    }

    private OrderProcessingResult ProcessUnderLock(OrderRequest order)
    {
        // Process already checked that this identifier is non-null and non-whitespace.
        string clOrdId = order.ClOrdId!;
        Snapshot state = _state;
        if (state.Orders.TryGetValue(clOrdId, out OriginalOrder? original))
        {
            return original.Order == order
                ? original.Result
                : OrderProcessingResult.Reject(order, OrderRejectionReason.IdentifierConflict);
        }

        _beforeCommit?.Invoke(OrderProcessingStage.BeforeValidation);

        OrderRejectionReason? reason = Validate(order);
        decimal candidateExposure = 0m;
        if (reason is null)
        {
            // The symbol, integral quantity and price were validated above.
            decimal currentExposure = state.Exposures.GetValueOrDefault(order.Symbol!);
            if (!order.ExactQuantity.TryToDecimal(out decimal quantity) ||
                order.ExactPrice is not { } exactPrice || !exactPrice.TryToDecimal(out decimal price))
            {
                throw new InvalidOperationException("Validated order numbers are not representable.");
            }

            decimal notional = quantity * price;
            candidateExposure = order.Side == OrderSide.Buy
                ? currentExposure + notional
                : currentExposure - notional;
            if (candidateExposure > MaximumExposure || candidateExposure < -MaximumExposure)
            {
                reason = OrderRejectionReason.ExposureLimitExceeded;
            }
        }

        OrderProcessingResult result = reason is null
            ? OrderProcessingResult.Accept(order)
            : OrderProcessingResult.Reject(order, reason.Value);
        _beforeCommit?.Invoke(OrderProcessingStage.AfterDecision);

        ImmutableDictionary<string, decimal> exposures = state.Exposures;
        if (result.Status == OrderProcessingStatus.Accepted)
        {
            exposures = exposures.SetItem(order.Symbol!, candidateExposure);
        }

        _beforeCommit?.Invoke(OrderProcessingStage.AfterExposurePreparation);
        ImmutableDictionary<string, OriginalOrder> orders =
            state.Orders.Add(clOrdId, new OriginalOrder(order, result));
        Snapshot next = new(exposures, orders);
        _beforeCommit?.Invoke(OrderProcessingStage.BeforePublication);

        // This is the only publication of identity, result, and exposure.
        _state = next;
        return result;
    }

    private static OrderRejectionReason? Validate(OrderRequest order)
    {
        if (!SupportedSymbols.IsSupported(order.Symbol))
        {
            return OrderRejectionReason.UnsupportedSymbol;
        }

        if (order.Side is not (OrderSide.Buy or OrderSide.Sell))
        {
            return OrderRejectionReason.InvalidSide;
        }

        if (!order.ExactQuantity.IsValidQuantity)
        {
            return OrderRejectionReason.InvalidQuantity;
        }

        if (!order.OrderType.IsLimit)
        {
            return OrderRejectionReason.UnsupportedOrderType;
        }

        if (order.ExactPrice is not { IsValidPrice: true })
        {
            return OrderRejectionReason.InvalidPrice;
        }

        return null;
    }

    private sealed record OriginalOrder(OrderRequest Order, OrderProcessingResult Result);

    private sealed record Snapshot(
        ImmutableDictionary<string, decimal> Exposures,
        ImmutableDictionary<string, OriginalOrder> Orders)
    {
        public static Snapshot Empty { get; } = new(
            ImmutableDictionary.Create<string, decimal>(StringComparer.Ordinal),
            ImmutableDictionary.Create<string, OriginalOrder>(StringComparer.Ordinal));
    }
}
