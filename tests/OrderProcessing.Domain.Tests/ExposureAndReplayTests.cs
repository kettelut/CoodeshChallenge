using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class ExposureAndReplayTests
{
    [Fact]
    public void Process_MixedOrders_AccumulatesExactNotionalAndIsolatesSymbols()
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create("buy1", quantity: 100m, price: 10m));
        TestOrders.Accepted(processor, TestOrders.Create("buy2", quantity: 50m, price: 12m));
        TestOrders.Accepted(processor, TestOrders.Create("sell", side: OrderSide.Sell, quantity: 80m, price: 11m));
        TestOrders.Accepted(processor, TestOrders.Create("decimal", "VALE3", OrderSide.Buy, 3m, 0.10m));
        TestOrders.Accepted(processor, TestOrders.Create("negative", "VIIA4", OrderSide.Sell, 7m, 0.01m));

        Assert.Equal(720m, processor.GetExposure("PETR4"));
        Assert.Equal(0.30m, processor.GetExposure("VALE3"));
        Assert.Equal(-0.07m, processor.GetExposure("VIIA4"));
        TestOrders.Accepted(processor, TestOrders.Create("zero", "VALE3", OrderSide.Sell, 3m, 0.10m));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
        Assert.Equal(720m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(OrderSide.Buy, "PETR4", 1)]
    [InlineData(OrderSide.Sell, "PETR4", -1)]
    [InlineData(OrderSide.Buy, "VALE3", 1)]
    [InlineData(OrderSide.Sell, "VALE3", -1)]
    [InlineData(OrderSide.Buy, "VIIA4", 1)]
    [InlineData(OrderSide.Sell, "VIIA4", -1)]
    public void Process_LimitBoundaries_AcceptsInsideAndExactlyAtLimitRejectsOneCentOutside(
        OrderSide side, string symbol, int sign)
    {
        var processor = new OrderProcessor();
        Assert.Equal(100_000_000m, OrderProcessor.MaximumExposure);
        TestOrders.SeedOneCentInsideLimit(processor, side, symbol);
        Assert.Equal(sign * 99_999_999.99m, processor.GetExposure(symbol));
        TestOrders.Accepted(processor, TestOrders.Create("at-limit", symbol, side, price: 0.01m));
        Assert.Equal(sign * 100_000_000m, processor.GetExposure(symbol));
        var rejected = TestOrders.Create("over-limit", symbol, side, price: 0.01m);
        OrderProcessingResult result = processor.Process(rejected);
        TestOrders.Rejected(result, OrderRejectionReason.ExposureLimitExceeded);
        Assert.Equal(sign * 100_000_000m, processor.GetExposure(symbol));
        TestOrders.SameDecision(result, processor.Process(rejected));
        Assert.Equal(sign * 100_000_000m, processor.GetExposure(symbol));
        OrderProcessingResult conflict = processor.Process(rejected with { Symbol = symbol == "PETR4" ? "VALE3" : "PETR4" });
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);

        OrderSide opposite = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        TestOrders.Accepted(processor, TestOrders.Create("reduce", symbol, opposite, price: 0.01m));
        Assert.Equal(sign * 99_999_999.99m, processor.GetExposure(symbol));
        TestOrders.SameDecision(result, processor.Process(rejected));
        Assert.Equal(sign * 99_999_999.99m, processor.GetExposure(symbol));
    }

    [Theory]
    [InlineData(OrderSide.Buy, OrderSide.Sell, 1)]
    [InlineData(OrderSide.Sell, OrderSide.Buy, -1)]
    public void Process_OppositeOrders_CrossZeroAndRetainSignedPrecision(OrderSide first, OrderSide second, int sign)
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create("first", side: first, quantity: 100m, price: 10m));
        TestOrders.Accepted(processor, TestOrders.Create("second", side: second, quantity: 200m, price: 10m));
        Assert.Equal(sign * -1000m, processor.GetExposure("PETR4"));
    }

    [Fact]
    public void Process_IdenticalReplayWithDifferentDecimalScale_ReproducesDecisionWithoutAnotherTransition()
    {
        var processor = new OrderProcessor();
        var order = TestOrders.Create(quantity: 3m, price: 1.10m);
        OrderProcessingResult original = TestOrders.Accepted(processor, order);
        OrderProcessingResult replay = processor.Process(order with { Quantity = 3.0m, Price = 1.1000m });
        TestOrders.SameDecision(original, replay);
        Assert.Equal(3.30m, processor.GetExposure("PETR4"));
    }

    [Fact]
    public void Process_CrossSymbolConflict_PreservesOriginalAndBothExposures()
    {
        var processor = new OrderProcessor();
        var originalOrder = TestOrders.Create("shared", quantity: 100m, price: 10m);
        OrderProcessingResult original = TestOrders.Accepted(processor, originalOrder);
        var conflicting = originalOrder with { Symbol = "VALE3" };
        OrderProcessingResult conflict = processor.Process(conflicting);
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        Assert.False(string.IsNullOrWhiteSpace(conflict.RejectionText));
        TestOrders.SameDecision(original, processor.Process(originalOrder));
        Assert.Equal(1000m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }

    [Theory]
    [InlineData("side")]
    [InlineData("quantity")]
    [InlineData("price")]
    public void Process_SameIdentifierWithChangedContent_ConflictsWithoutReplacingOriginal(string changedField)
    {
        var processor = new OrderProcessor();
        var order = TestOrders.Create("shared", quantity: 3m, price: 1.10m);
        OrderProcessingResult original = TestOrders.Accepted(processor, order);
        OrderRequest conflictOrder = changedField switch
        {
            "side" => order with { Side = OrderSide.Sell },
            "quantity" => order with { Quantity = 4m },
            "price" => order with { Price = 1.11m },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };

        OrderProcessingResult conflict = processor.Process(conflictOrder);

        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        TestOrders.SameDecision(original, processor.Process(order));
        Assert.Equal(3.30m, processor.GetExposure("PETR4"));
    }
}
