namespace OrderProcessing.Domain;

/// <summary>A transport-neutral submission, including exact, potentially invalid numeric values.</summary>
/// <remarks>
/// Identity content consists of Symbol, Side, Quantity, Price and OrderType. Numbers
/// compare by exact numeric value; an absent price differs from a present price.
/// Transport metadata and the original numeric spelling are not order content.
/// </remarks>
public sealed record OrderRequest
{
    private ExactNumber _quantity;
    private ExactNumber? _price;

    // Retains the original decimal API for callers that already have exact decimal values.
    public OrderRequest(
        string? ClOrdId,
        string? Symbol,
        OrderSide Side,
        decimal Quantity,
        decimal? Price,
        OrderType OrderType = default)
        : this(ClOrdId, Symbol, Side, ExactNumber.FromDecimal(Quantity),
            Price is decimal price ? ExactNumber.FromDecimal(price) : null, OrderType)
    {
    }

    private OrderRequest(
        string? ClOrdId,
        string? Symbol,
        OrderSide Side,
        ExactNumber Quantity,
        ExactNumber? Price,
        OrderType OrderType = default)
    {
        ArgumentNullException.ThrowIfNull(Quantity);
        this.ClOrdId = ClOrdId;
        this.Symbol = Symbol;
        this.Side = Side;
        _quantity = Quantity;
        _price = Price;
        this.OrderType = OrderType;
    }

    /// <summary>
    /// Constructs a request from exact numbers parsed before any lossy transport conversion.
    /// A null price represents an absent field, not a malformed present field.
    /// </summary>
    public static OrderRequest FromExactNumbers(
        string? ClOrdId,
        string? Symbol,
        OrderSide Side,
        ExactNumber Quantity,
        ExactNumber? Price,
        OrderType OrderType = default) =>
        new(ClOrdId, Symbol, Side, Quantity, Price, OrderType);

    public string? ClOrdId { get; init; }
    public string? Symbol { get; init; }
    public OrderSide Side { get; init; }
    public OrderType OrderType { get; init; }

    /// <summary>The exact submitted quantity, including its original spelling.</summary>
    public ExactNumber ExactQuantity => _quantity;

    /// <summary>The exact submitted price, or null when absent.</summary>
    public ExactNumber? ExactPrice => _price;

    /// <summary>
    /// Compatibility accessor. Throws OverflowException when the exact value is not
    /// representable as decimal; use ExactQuantity for arbitrary input and reports.
    /// </summary>
    public decimal Quantity
    {
        get => _quantity.TryToDecimal(out decimal value)
            ? value
            : throw new OverflowException("Quantity cannot be represented exactly as decimal.");
        init => _quantity = ExactNumber.FromDecimal(value);
    }

    /// <summary>
    /// Compatibility accessor. Throws OverflowException when a present price is not
    /// representable as decimal; use ExactPrice for arbitrary input and reports.
    /// </summary>
    public decimal? Price
    {
        get => _price is null ? null : _price.TryToDecimal(out decimal value)
            ? value
            : throw new OverflowException("Price cannot be represented exactly as decimal.");
        init => _price = value is decimal price ? ExactNumber.FromDecimal(price) : null;
    }

    // The synthesized record formatter would call the decimal compatibility accessors.
    public override string ToString() =>
        $"OrderRequest {{ ClOrdId = {ClOrdId}, Symbol = {Symbol}, Side = {Side}, " +
        $"Quantity = {ExactQuantity}, Price = {ExactPrice}, OrderType = {OrderType} }}";

    public void Deconstruct(
        out string? ClOrdId, out string? Symbol, out OrderSide Side,
        out decimal Quantity, out decimal? Price, out OrderType OrderType)
    {
        ClOrdId = this.ClOrdId;
        Symbol = this.Symbol;
        Side = this.Side;
        Quantity = this.Quantity;
        Price = this.Price;
        OrderType = this.OrderType;
    }
}
