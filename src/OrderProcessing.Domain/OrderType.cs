namespace OrderProcessing.Domain;

/// <summary>
/// A Limit order, or an unsupported order type with its original, unmodified value.
/// </summary>
/// <remarks>
/// Adapters must map the supported type to <see cref="Limit"/> and pass each other
/// type's distinct value to <see cref="Unsupported"/> without normalization.
/// The default value is Limit for compatibility with existing submissions.
/// </remarks>
public readonly record struct OrderType
{
    private OrderType(string unsupportedValue) => UnsupportedValue = unsupportedValue;

    public static OrderType Limit => default;

    public static OrderType Unsupported(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new OrderType(value);
    }

    public string? UnsupportedValue { get; }
    public bool IsLimit => UnsupportedValue is null;
}
