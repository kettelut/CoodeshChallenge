using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

internal static class TestOrders
{
    public static OrderRequest Create(
        string id = "order",
        string? symbol = "PETR4",
        OrderSide side = OrderSide.Buy,
        decimal quantity = 1m,
        decimal price = 1m) => new(id, symbol, side, quantity, price);

    public static OrderProcessingResult Accepted(OrderProcessor processor, OrderRequest order)
    {
        OrderProcessingResult result = processor.Process(order);
        Assert.Equal(OrderProcessingStatus.Accepted, result.Status);
        Assert.Null(result.RejectionReason);
        return result;
    }

    public static void Rejected(OrderProcessingResult result, OrderRejectionReason reason)
    {
        Assert.Equal(OrderProcessingStatus.Rejected, result.Status);
        Assert.Equal(reason, result.RejectionReason);
        Assert.False(string.IsNullOrWhiteSpace(result.RejectionText));
    }

    public static void SameDecision(OrderProcessingResult expected, OrderProcessingResult actual)
    {
        Assert.False(string.IsNullOrWhiteSpace(expected.OrderId));
        Assert.Equal(expected.OrderId, actual.OrderId);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.RejectionReason, actual.RejectionReason);
        Assert.Equal(expected.RejectionText, actual.RejectionText);
        Assert.Equal(expected.Order, actual.Order);
    }

    // Two valid orders produce 99,999,999.99 without any test-only state mutation.
    public static void SeedOneCentInsideLimit(OrderProcessor processor, OrderSide side, string symbol = "PETR4")
    {
        Accepted(processor, Create("seed-large-" + symbol, symbol, side, 99_999m, 999.99m));
        Accepted(processor, Create("seed-small-" + symbol, symbol, side, 2m, 999.99m));
    }
}
