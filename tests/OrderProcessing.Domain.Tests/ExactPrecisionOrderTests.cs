using System.Globalization;
using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class ExactPrecisionOrderTests
{
    private const string RoundedOneA = "1.00000000000000000000000000001";
    private const string RoundedOneB = "1.00000000000000000000000000002";

    [Theory]
    [InlineData("1.10000000000000000000000000001")]
    [InlineData("0.01000000000000000000000000001")]
    [InlineData("999.99000000000000000000000000001")]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("-0.00000000000000000000000000001")]
    [InlineData("-1.10000000000000000000000000001")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("-9999999999999999999999999999999999999999999999999999999999")]
    [InlineData("1000.00000000000000000000000000001")]
    public void Process_InvalidExactPrice_RejectsReplaysAndPreservesExistingExposure(string price)
    {
        var processor = SeededProcessor();
        OrderRequest order = Create("invalid-price", "3", price);

        OrderProcessingResult original = processor.Process(order);
        OrderProcessingResult replay = processor.Process(Create("invalid-price", "3.000", WithTrailingZero(price)));

        TestOrders.Rejected(original, OrderRejectionReason.InvalidPrice);
        TestOrders.SameDecision(original, replay);
        Assert.Equal(price, original.Order.ExactPrice!.RawText);
        AssertExposuresUnchanged(processor);
    }

    [Theory]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("99999.00000000000000000000000000001")]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("-0.00000000000000000000000000001")]
    [InlineData("-1.00000000000000000000000000001")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("-9999999999999999999999999999999999999999999999999999999999")]
    [InlineData("100000.00000000000000000000000000001")]
    public void Process_InvalidExactQuantity_RejectsReplaysAndPreservesExistingExposure(string quantity)
    {
        var processor = SeededProcessor();
        OrderRequest order = Create("invalid-quantity", quantity, "1.10");

        OrderProcessingResult original = processor.Process(order);
        OrderProcessingResult replay = processor.Process(Create("invalid-quantity", WithTrailingZero(quantity), "1.1000"));

        TestOrders.Rejected(original, OrderRejectionReason.InvalidQuantity);
        TestOrders.SameDecision(original, replay);
        Assert.Equal(quantity, original.Order.ExactQuantity.RawText);
        AssertExposuresUnchanged(processor);
    }

    [Theory]
    [InlineData("1.10000000000000000000000000001", "1.10")]
    [InlineData("0.01000000000000000000000000001", "0.01")]
    [InlineData("999.99000000000000000000000000001", "999.99")]
    public void Process_ExactPriceThatDecimalRoundsToValid_RejectsInsteadOfAccepting(string text, string expected)
    {
        // Decimal parsing illustrates why validation must happen before lossy conversion.
        decimal rounded = decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        var processor = new OrderProcessor();

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), rounded);
        Assert.True(rounded > 0m && rounded < 1000m && rounded % 0.01m == 0m);
        TestOrders.Rejected(processor.Process(Create("exact", "1", text)), OrderRejectionReason.InvalidPrice);
        Assert.Equal(0m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("1.00000000000000000000000000001", 1)]
    [InlineData("99999.00000000000000000000000000001", 99999)]
    public void Process_ExactQuantityThatDecimalRoundsToValid_RejectsInsteadOfAccepting(string text, int expected)
    {
        decimal rounded = decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        var processor = new OrderProcessor();

        Assert.Equal((decimal)expected, rounded);
        TestOrders.Rejected(processor.Process(Create("exact", text, "1")), OrderRejectionReason.InvalidQuantity);
        Assert.Equal(0m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(OrderSide.Buy, 1)]
    [InlineData(OrderSide.Sell, -1)]
    public void Process_ValidExactValuesWithLongTrailingZeros_AcceptsFullSignedNotional(OrderSide side, int sign)
    {
        var processor = new OrderProcessor();
        string zeros = new('0', 1000);
        OrderRequest order = Create("exact", "0003." + zeros, "0001.10" + zeros, side);

        OrderProcessingResult result = TestOrders.Accepted(processor, order);

        Assert.Equal(3m, result.Order.Quantity);
        Assert.Equal(1.10m, result.Order.Price);
        Assert.Equal(sign * 3.30m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }

    [Fact]
    public void Process_ExactBusinessBoundariesWithLongTrailingZeros_AcceptsWithoutRounding()
    {
        var processor = new OrderProcessor();
        string zeros = new('0', 100);
        OrderRequest maximum = Create("maximum", "99999." + zeros, "999.99" + zeros);
        OrderRequest minimum = Create("minimum", "1." + zeros, "0.01" + zeros);

        TestOrders.Accepted(processor, maximum);
        Assert.Equal(99_998_000.01m, processor.GetExposure("PETR4"));
        TestOrders.Accepted(processor, minimum);
        Assert.Equal(99_998_000.02m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_ExactAndDecimalNumericEquals_ReplaysOriginalInBothDirections(bool decimalFirst)
    {
        var processor = new OrderProcessor();
        OrderRequest exact = Create("same", "0003." + new string('0', 100),
            "1.1" + new string('0', 100));
        var decimalOrder = new OrderRequest("same", "PETR4", OrderSide.Buy, 3m, 1.1000m);
        OrderRequest first = decimalFirst ? decimalOrder : exact;
        OrderRequest second = decimalFirst ? exact : decimalOrder;

        OrderProcessingResult original = TestOrders.Accepted(processor, first);
        OrderProcessingResult replay = processor.Process(second);

        TestOrders.SameDecision(original, replay);
        Assert.Equal(3.30m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Process_SameIdDifferentExactNumbersThatRoundAlike_ConflictsAndRetainsOriginal(
        bool quantityField, bool validFirst)
    {
        Assert.Equal(1m, decimal.Parse(RoundedOneA, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture));
        Assert.Equal(1m, decimal.Parse(RoundedOneB, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture));
        var processor = SeededProcessor();
        string first = validFirst ? "1" : RoundedOneA;
        string changed = validFirst ? RoundedOneA : RoundedOneB;
        OrderRequest originalOrder = quantityField
            ? Create("shared", first, "1")
            : Create("shared", "1", first);
        OrderRequest conflicting = quantityField
            ? Create("shared", changed, "1")
            : Create("shared", "1", changed);

        OrderProcessingResult original = processor.Process(originalOrder);
        OrderProcessingResult conflict = processor.Process(conflicting);

        if (validFirst)
        {
            Assert.Equal(OrderProcessingStatus.Accepted, original.Status);
            Assert.Null(original.RejectionReason);
        }
        else
        {
            TestOrders.Rejected(original, quantityField
                ? OrderRejectionReason.InvalidQuantity
                : OrderRejectionReason.InvalidPrice);
        }
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        Assert.False(string.IsNullOrWhiteSpace(conflict.RejectionText));
        TestOrders.SameDecision(original, processor.Process(originalOrder));
        Assert.Equal(validFirst ? 8m : 7m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Process_RejectedExactOriginalFollowedByRoundedValidContent_ConflictsAndReplaysOriginal(
        bool quantityField)
    {
        var processor = SeededProcessor();
        OrderRequest originalOrder = quantityField
            ? Create("shared", RoundedOneA, "1")
            : Create("shared", "1", RoundedOneA);
        OrderRequest roundedOrder = Create("shared", "1", "1");
        OrderRequest equivalentReplay = quantityField
            ? Create("shared", RoundedOneA + "0", "1.00")
            : Create("shared", "1.00", RoundedOneA + "0");

        OrderProcessingResult original = processor.Process(originalOrder);
        OrderProcessingResult conflict = processor.Process(roundedOrder);

        TestOrders.Rejected(original, quantityField
            ? OrderRejectionReason.InvalidQuantity
            : OrderRejectionReason.InvalidPrice);
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        TestOrders.SameDecision(original, processor.Process(equivalentReplay));
        AssertExposuresUnchanged(processor);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompatibilityAccessors_InexactInput_ThrowsRatherThanReturningRoundedDecimal(bool quantityField)
    {
        OrderRequest order = quantityField
            ? Create("exact", RoundedOneA, "1")
            : Create("exact", "1", RoundedOneA);

        if (quantityField)
        {
            Assert.Throws<OverflowException>(() => order.Quantity);
            Assert.Equal(RoundedOneA, order.ExactQuantity.RawText);
        }
        else
        {
            Assert.Throws<OverflowException>(() => order.Price);
            Assert.Equal(RoundedOneA, order.ExactPrice!.RawText);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_AbsentPriceAndPresentZero_AreDistinctEvenThoughBothRejected(bool absentFirst)
    {
        var processor = SeededProcessor();
        OrderRequest absent = Create("shared", "3", null);
        OrderRequest presentZero = Create("shared", "3.000", "0." + new string('0', 100));
        OrderRequest first = absentFirst ? absent : presentZero;
        OrderRequest second = absentFirst ? presentZero : absent;

        OrderProcessingResult original = processor.Process(first);
        OrderProcessingResult conflict = processor.Process(second);

        TestOrders.Rejected(original, OrderRejectionReason.InvalidPrice);
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        TestOrders.SameDecision(original, processor.Process(first));
        AssertExposuresUnchanged(processor);
    }

    private static OrderRequest Create(string id, string quantity, string? price, OrderSide side = OrderSide.Buy) =>
        OrderRequest.FromExactNumbers(id, "PETR4", side, ExactNumberTests.Parse(quantity),
            price is null ? null : ExactNumberTests.Parse(price));

    private static string WithTrailingZero(string number) =>
        number.Contains('.') ? number + "0" : number + ".0";

    private static OrderProcessor SeededProcessor()
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create("seed", price: 7m));
        return processor;
    }

    private static void AssertExposuresUnchanged(OrderProcessor processor)
    {
        Assert.Equal(7m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
        Assert.Equal(0m, processor.GetExposure("VIIA4"));
    }
}
