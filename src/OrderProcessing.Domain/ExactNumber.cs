using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OrderProcessing.Domain;

/// <summary>Failure to read a numeric field, not a business rejection.</summary>
public enum NumericInputError
{
    None,
    Missing,
    Malformed,
    TooLong
}

/// <summary>
/// An exact, finite base-10 value, including values outside the range or precision of decimal.
/// Equality and hashing use the numeric value, not its original spelling.
/// </summary>
public sealed class ExactNumber : IEquatable<ExactNumber>
{
    // Bound the work and retained memory for untrusted numeric fields.
    public const int MaximumInputLength = 4096;

    private readonly string _canonical;

    private ExactNumber(string rawText, string canonical)
    {
        RawText = rawText;
        _canonical = canonical;
    }

    /// <summary>The original field text, suitable for lossless report mapping.</summary>
    public string RawText { get; }

    /// <summary>
    /// Reads ASCII digits with an optional leading minus and at most one decimal point.
    /// At least one digit is required; whitespace, plus signs and exponents are not accepted.
    /// The caller must separately enforce transport-specific required fields and syntax.
    /// </summary>
    public static bool TryParse(
        string? text, [NotNullWhen(true)] out ExactNumber? value, out NumericInputError error)
    {
        value = null;
        if (text is null)
        {
            error = NumericInputError.Missing;
            return false;
        }

        if (text.Length > MaximumInputLength)
        {
            error = NumericInputError.TooLong;
            return false;
        }

        int start = text.StartsWith('-') ? 1 : 0;
        int point = -1;
        bool hasDigit = false;
        for (int i = start; i < text.Length; i++)
        {
            char character = text[i];
            if (character is >= '0' and <= '9')
            {
                hasDigit = true;
            }
            else if (character == '.' && point < 0)
            {
                point = i;
            }
            else
            {
                error = NumericInputError.Malformed;
                return false;
            }
        }

        if (!hasDigit)
        {
            error = NumericInputError.Malformed;
            return false;
        }

        int integerEnd = point < 0 ? text.Length : point;
        int integerStart = start;
        while (integerStart < integerEnd && text[integerStart] == '0')
        {
            integerStart++;
        }

        int fractionStart = point < 0 ? text.Length : point + 1;
        int fractionEnd = text.Length;
        while (fractionEnd > fractionStart && text[fractionEnd - 1] == '0')
        {
            fractionEnd--;
        }

        bool zero = integerStart == integerEnd && fractionStart == fractionEnd;
        string integer = integerStart == integerEnd ? "0" : text[integerStart..integerEnd];
        string fraction = fractionStart == fractionEnd ? "" : "." + text[fractionStart..fractionEnd];
        string canonical = (start == 1 && !zero ? "-" : "") + integer + fraction;
        value = new ExactNumber(text, canonical);
        error = NumericInputError.None;
        return true;
    }

    /// <summary>Converts a decimal without changing its numeric value.</summary>
    public static ExactNumber FromDecimal(decimal value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        if (!TryParse(text, out ExactNumber? number, out _))
        {
            throw new InvalidOperationException("Could not represent a decimal as an exact number.");
        }

        return number;
    }

    /// <summary>
    /// Converts only if decimal represents the value exactly; never returns a rounded value.
    /// </summary>
    public bool TryToDecimal(out decimal value)
    {
        if (decimal.TryParse(_canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value) && Equals(FromDecimal(value)))
        {
            return true;
        }

        value = default;
        return false;
    }

    internal bool IsValidQuantity =>
        IsPositive && !_canonical.Contains('.') && _canonical.Length <= 5;

    internal bool IsValidPrice
    {
        get
        {
            if (!IsPositive)
            {
                return false;
            }

            int point = _canonical.IndexOf('.');
            int integerLength = point < 0 ? _canonical.Length : point;
            int fractionalLength = point < 0 ? 0 : _canonical.Length - point - 1;
            return integerLength <= 3 && fractionalLength <= 2;
        }
    }

    private bool IsPositive => _canonical != "0" && _canonical[0] != '-';

    /// <summary>A normalized invariant representation with no insignificant zeroes.</summary>
    public override string ToString() => _canonical;

    public bool Equals(ExactNumber? other) =>
        other is not null && StringComparer.Ordinal.Equals(_canonical, other._canonical);

    public override bool Equals(object? obj) => obj is ExactNumber other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_canonical);

    public static bool operator ==(ExactNumber? left, ExactNumber? right) =>
        EqualityComparer<ExactNumber>.Default.Equals(left, right);

    public static bool operator !=(ExactNumber? left, ExactNumber? right) => !(left == right);
}
