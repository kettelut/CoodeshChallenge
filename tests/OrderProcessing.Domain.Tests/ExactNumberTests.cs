using System.Globalization;
using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class ExactNumberTests
{
    [Theory]
    [InlineData(null, NumericInputError.Missing)]
    [InlineData("", NumericInputError.Malformed)]
    [InlineData("-", NumericInputError.Malformed)]
    [InlineData(".", NumericInputError.Malformed)]
    [InlineData("-.", NumericInputError.Malformed)]
    [InlineData("+1", NumericInputError.Malformed)]
    [InlineData(" 1", NumericInputError.Malformed)]
    [InlineData("1 ", NumericInputError.Malformed)]
    [InlineData("1e2", NumericInputError.Malformed)]
    [InlineData("1,1", NumericInputError.Malformed)]
    [InlineData("1.2.3", NumericInputError.Malformed)]
    [InlineData("--1", NumericInputError.Malformed)]
    [InlineData("١", NumericInputError.Malformed)]
    public void TryParse_MissingOrMalformedInput_ReturnsSpecificError(string? text, NumericInputError expected)
    {
        bool parsed = ExactNumber.TryParse(text, out ExactNumber? value, out NumericInputError error);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void TryParse_InputLengthBoundary_AcceptsMaximumAndRejectsLonger()
    {
        string maximum = "1." + new string('0', ExactNumber.MaximumInputLength - 2);
        string tooLong = maximum + "0";

        ExactNumber number = Parse(maximum);
        Assert.Equal(maximum, number.RawText);
        Assert.Equal("1", number.ToString());
        Assert.True(number.TryToDecimal(out decimal value));
        Assert.Equal(1m, value);
        Assert.False(ExactNumber.TryParse(tooLong, out ExactNumber? rejected, out NumericInputError error));
        Assert.Null(rejected);
        Assert.Equal(NumericInputError.TooLong, error);
    }

    [Theory]
    [InlineData("1.1", "0001.1000000000000000000000000000000000000000")]
    [InlineData("0", "-000.0000000000000000000000000000000000000000")]
    [InlineData("-.01", "-0.0100000000000000000000000000000000000000")]
    public void Equality_DifferentSpellingsOfSameNumber_AgreesAndHashesEqually(string first, string second)
    {
        ExactNumber left = Parse(first);
        ExactNumber right = Parse(second);

        Assert.Equal(first, left.RawText);
        Assert.Equal(second, right.RawText);
        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData("1.00000000000000000000000000001", 1)]
    [InlineData("0.00000000000000000000000000001", 0)]
    [InlineData("-0.00000000000000000000000000001", 0)]
    public void TryToDecimal_WhenDecimalWouldRound_ReturnsFalse(string text, int rounded)
    {
        ExactNumber number = Parse(text);
        decimal roundedValue = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);

        Assert.Equal((decimal)rounded, roundedValue);
        Assert.False(number.TryToDecimal(out decimal value));
        Assert.Equal(0m, value);
        Assert.NotEqual(ExactNumber.FromDecimal(roundedValue), number);
    }

    [Theory]
    [InlineData("79228162514264337593543950336")]
    [InlineData("-79228162514264337593543950336")]
    [InlineData("9999999999999999999999999999999999999999999999999999999999")]
    public void TryToDecimal_OutsideDecimalRange_ReturnsFalseWithoutLosingRawText(string text)
    {
        ExactNumber number = Parse(text);

        Assert.Equal(text, number.RawText);
        Assert.False(number.TryToDecimal(out decimal value));
        Assert.Equal(0m, value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FromDecimal_RangeLimits_RoundTripExactly(bool positive)
    {
        decimal expected = positive ? decimal.MaxValue : decimal.MinValue;
        ExactNumber number = ExactNumber.FromDecimal(expected);

        Assert.True(number.TryToDecimal(out decimal actual));
        Assert.Equal(expected, actual);
    }

    internal static ExactNumber Parse(string text)
    {
        Assert.True(ExactNumber.TryParse(text, out ExactNumber? number, out NumericInputError error));
        Assert.Equal(NumericInputError.None, error);
        return Assert.IsType<ExactNumber>(number);
    }
}
