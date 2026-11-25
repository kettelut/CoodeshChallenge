using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class OrderValidationTests
{
    [Theory]
    [InlineData("PETR4")]
    [InlineData("VALE3")]
    [InlineData("VIIA4")]
    public void Process_SupportedSymbol_Accepts(string symbol)
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create(symbol: symbol));
        Assert.Equal(1m, processor.GetExposure(symbol));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ITUB4")]
    [InlineData("petr4")]
    [InlineData(" PETR4")]
    [InlineData("PETR4 ")]
    public void Process_UnsupportedSymbol_RejectsWithoutExposure(string? symbol)
    {
        var processor = new OrderProcessor();
        TestOrders.Rejected(processor.Process(TestOrders.Create(symbol: symbol)), OrderRejectionReason.UnsupportedSymbol);
        Assert.Equal(0m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
        Assert.Equal(0m, processor.GetExposure("VIIA4"));
    }

    [Theory]
    [InlineData(OrderSide.Buy, 1)]
    [InlineData(OrderSide.Sell, -1)]
    public void Process_SupportedSide_AppliesSignedExposure(OrderSide side, int sign)
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create(side: side));
        Assert.Equal(sign * 1m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void Process_InvalidSide_RejectsWithoutExposure(int side)
    {
        var processor = new OrderProcessor();
        TestOrders.Rejected(processor.Process(TestOrders.Create(side: (OrderSide)side)), OrderRejectionReason.InvalidSide);
        Assert.Equal(0m, processor.GetExposure("PETR4"));
    }

    public static TheoryData<decimal, bool> Quantities => new()
    {
        { 1m, true }, { 99_999m, true }, { 0m, false }, { -1m, false },
        { 100_000m, false }, { 1.5m, false }
    };

    [Theory]
    [MemberData(nameof(Quantities))]
    public void Process_QuantityBoundaries_ValidatesWithoutTruncation(decimal quantity, bool valid)
    {
        var processor = new OrderProcessor();
        OrderProcessingResult result = processor.Process(TestOrders.Create(quantity: quantity));
        if (valid)
        {
            Assert.Equal(OrderProcessingStatus.Accepted, result.Status);
            Assert.Null(result.RejectionReason);
            Assert.Equal(quantity, processor.GetExposure("PETR4"));
        }
        else
        {
            TestOrders.Rejected(result, OrderRejectionReason.InvalidQuantity);
            Assert.Equal(0m, processor.GetExposure("PETR4"));
        }
    }

    public static TheoryData<decimal, bool> Prices => new()
    {
        { 0.01m, true }, { 999.99m, true }, { 1.10m, true }, { 1.100m, true },
        { 1.1000m, true }, { 0.001m, false }, { 1.101m, false }, { 1.105m, false },
        { 0m, false }, { -0.01m, false }, { 1000m, false }, { 1000.01m, false },
        { 10.001m, false }, { 10.005m, false }
    };

    [Theory]
    [MemberData(nameof(Prices))]
    public void Process_PriceBoundaries_UsesNumericCentPrecision(decimal price, bool valid)
    {
        var processor = new OrderProcessor();
        OrderProcessingResult result = processor.Process(TestOrders.Create(quantity: 3m, price: price));
        if (valid)
        {
            Assert.Equal(OrderProcessingStatus.Accepted, result.Status);
            Assert.Null(result.RejectionReason);
            Assert.Equal(price * 3m, processor.GetExposure("PETR4"));
        }
        else
        {
            TestOrders.Rejected(result, OrderRejectionReason.InvalidPrice);
            Assert.Equal(0m, processor.GetExposure("PETR4"));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Process_MissingIdentifier_RejectsWithoutRegisteringOrChangingExposure(string? id)
    {
        var processor = new OrderProcessor();
        var order = TestOrders.Create() with { ClOrdId = id };
        TestOrders.Rejected(processor.Process(order), OrderRejectionReason.InvalidClOrdId);
        TestOrders.Rejected(processor.Process(order with { Symbol = "VALE3" }), OrderRejectionReason.InvalidClOrdId);
        Assert.Equal(0m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }
}
