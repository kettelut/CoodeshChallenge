namespace OrderProcessing.Domain;

/// <summary>Symbols are matched exactly and are not normalized.</summary>
public static class SupportedSymbols
{
    public const string Petr4 = "PETR4";
    public const string Vale3 = "VALE3";
    public const string Viia4 = "VIIA4";

    public static bool IsSupported(string? symbol) =>
        symbol is Petr4 or Vale3 or Viia4;
}
