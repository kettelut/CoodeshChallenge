using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderGenerator.Application;
using OrderProcessing.Domain;

namespace OrderGenerator.Pages;

public sealed class IndexModel(
    IOrderSubmissionService submissionService,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<IndexModel> logger) : PageModel
{
    private const int MaximumConsumedTokens = 10_000;
    private static readonly string ProcessPurpose = Guid.NewGuid().ToString("N");
    private static readonly Lock TokenLock = new();
    private static readonly Dictionary<Guid, DateTimeOffset> ConsumedTokens = [];
    private readonly ITimeLimitedDataProtector _tokenProtector = dataProtectionProvider
        .CreateProtector("OrderGenerator.OrderForm." + ProcessPurpose).ToTimeLimitedDataProtector();

    [BindProperty]
    public string? Symbol { get; set; }

    [BindProperty]
    public string? Side { get; set; }

    [BindProperty]
    public string? Quantity { get; set; }

    [BindProperty]
    public string? Price { get; set; }

    [BindProperty]
    public string? SubmissionToken { get; set; }

    public bool SubmissionBlocked { get; private set; }
    public OrderResultView? Result { get; private set; }

    public void OnGet()
    {
        Response.Headers.CacheControl = "no-store";
        SubmissionToken = _tokenProtector.Protect(Guid.NewGuid().ToString("N"), TimeSpan.FromHours(1));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!SupportedSymbols.IsSupported(Symbol))
            ModelState.AddModelError(nameof(Symbol), "Selecione um símbolo disponível.");

        OrderSide side = Side switch
        {
            "Buy" => OrderSide.Buy,
            "Sell" => OrderSide.Sell,
            _ => default
        };
        if (side is not (OrderSide.Buy or OrderSide.Sell))
            ModelState.AddModelError(nameof(Side), "Selecione compra ou venda.");

        if (!int.TryParse(Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out int quantity)
            || quantity is < 1 or >= 100_000)
            ModelState.AddModelError(nameof(Quantity), "Informe um inteiro entre 1 e 99999.");

        decimal price = 0m;
        if (!ExactNumber.TryParse(Price, out var exactPrice, out _)
            || !exactPrice.TryToDecimal(out price)
            || price <= 0m || price >= 1000m || price % 0.01m != 0m)
            ModelState.AddModelError(nameof(Price), "Informe um preço maior que 0 e menor que 1000, em múltiplos de 0.01.");

        if (!ModelState.IsValid)
            return Page();

        var tokenStatus = TryConsumeSubmissionToken();
        if (tokenStatus != TokenConsumptionStatus.Consumed)
        {
            if (tokenStatus == TokenConsumptionStatus.InvalidOrUsed)
            {
                SubmissionBlocked = true;
                ModelState.AddModelError(string.Empty,
                    "Este formulário já foi enviado, expirou ou é inválido. "
                    + "O resultado de um envio anterior pode ser desconhecido; confira antes de criar outra ordem.");
            }
            else
            {
                ModelState.AddModelError(string.Empty,
                    "A capacidade global de envio está esgotada. Nenhuma ordem foi enviada nesta tentativa. "
                    + "Aguarde e tente novamente manualmente; confira qualquer resultado anterior desconhecido.");
            }
            return Page();
        }

        // Keep the consumed token on the response: a refresh or replay must not submit another order.
        SubmissionBlocked = true;
        SubmissionResult? result;
        try
        {
            result = await submissionService.SubmitAsync(
                new OrderSubmission(Symbol, side, quantity, price), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError("Unexpected order submission failure; error type {ErrorType}; outcome may be unknown",
                exception.GetType().Name);
            result = new SubmissionResult(SubmissionStatus.TechnicalFailure, null,
                "Não foi possível confirmar o resultado da ordem.", true);
        }

        if (result is null)
        {
            result = new SubmissionResult(SubmissionStatus.TechnicalFailure, null,
                "A resposta do serviço está ausente.", true);
        }
        else if ((result.Status is SubmissionStatus.Accepted or SubmissionStatus.Rejected)
            && (result.OutcomeMayBeUnknown || string.IsNullOrWhiteSpace(result.ClOrdId)
                || (result.Status == SubmissionStatus.Accepted
                    && (string.IsNullOrWhiteSpace(result.OrderId) || string.IsNullOrWhiteSpace(result.ExecId)))
                || (result.Status == SubmissionStatus.Rejected && string.IsNullOrWhiteSpace(result.Explanation))))
        {
            result = new SubmissionResult(SubmissionStatus.TechnicalFailure, result.ClOrdId,
                "A resposta do serviço está incompleta ou é inconsistente.", true);
        }

        // Bound untrusted report text before rendering it with Razor's HTML encoding.
        Result = new OrderResultView(
            result.Status, Limit(result.ClOrdId, 256), Limit(result.Explanation, 800),
            result.OutcomeMayBeUnknown, Limit(result.OrderId, 256), Limit(result.ExecId, 256),
            result.RejectionReason, Symbol!, side, quantity, price);
        return Page();
    }

    private TokenConsumptionStatus TryConsumeSubmissionToken()
    {
        if (string.IsNullOrEmpty(SubmissionToken) || SubmissionToken.Length > 4096)
            return TokenConsumptionStatus.InvalidOrUsed;

        string value;
        DateTimeOffset expires;
        try
        {
            value = _tokenProtector.Unprotect(SubmissionToken, out expires);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return TokenConsumptionStatus.InvalidOrUsed;
        }

        if (!Guid.TryParseExact(value, "N", out Guid id))
            return TokenConsumptionStatus.InvalidOrUsed;

        lock (TokenLock)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (Guid expired in ConsumedTokens.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
                ConsumedTokens.Remove(expired);

            if (expires <= now || ConsumedTokens.ContainsKey(id))
                return TokenConsumptionStatus.InvalidOrUsed;

            if (ConsumedTokens.Count >= MaximumConsumedTokens)
                return TokenConsumptionStatus.CapacityExceeded;

            ConsumedTokens.Add(id, expires);
            return TokenConsumptionStatus.Consumed;
        }
    }

    private enum TokenConsumptionStatus
    {
        Consumed,
        InvalidOrUsed,
        CapacityExceeded
    }

    private static string? Limit(string? text, int length) =>
        text is { Length: > 0 } && text.Length > length ? text[..length] + "… (texto truncado)" : text;

    public sealed record OrderResultView(
        SubmissionStatus Status,
        string? ClOrdId,
        string? Explanation,
        bool OutcomeMayBeUnknown,
        string? OrderId,
        string? ExecId,
        int? RejectionReason,
        string Symbol,
        OrderSide Side,
        int Quantity,
        decimal Price);
}
