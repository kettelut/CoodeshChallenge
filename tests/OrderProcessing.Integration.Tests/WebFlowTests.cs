extern alias generator;

using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderGenerator.Application;
using OrderProcessing.Domain;
using QuickFix;
using QuickFix.Fields;
using Xunit;

namespace OrderProcessing.Integration.Tests;

public sealed class WebFlowTests
{
    [Fact]
    public async Task RazorForm_AntiforgeryAndServerValidation_BlockInvalidOrders()
    {
        await using var harness = await WebHarness.StartAsync();
        using HttpClient client = harness.CreateClient();
        string html = await client.GetStringAsync("/");
        Assert.Contains("Gerador de Ordens", html);

        using var unprotected = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Symbol"] = "PETR4", ["Side"] = "Buy", ["Quantity"] = "1", ["Price"] = "10.00"
        });
        using HttpResponseMessage denied = await client.PostAsync("/", unprotected);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        using HttpResponseMessage invalid = await PostAsync(client, html, "PETR4", "Buy", "100000", "1.101");
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        string error = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Informe um inteiro entre 1 e 99999", error);
        Assert.Contains("múltiplos de 0.01", WebUtility.HtmlDecode(error));
        Assert.Equal(0m, harness.Fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task RazorForm_RealFixFlow_DisplaysAcceptedAndExposureRejection()
    {
        await using var harness = await WebHarness.StartAsync();
        using HttpClient client = harness.CreateClient();
        string form = await client.GetStringAsync("/");
        using HttpResponseMessage accepted = await PostAsync(client, form, "PETR4", "Buy", "99999", "999.99");
        string acceptedHtml = WebUtility.HtmlDecode(await accepted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Null(accepted.Headers.Location);
        Assert.Contains("Ordem aceita", acceptedHtml);
        Assert.Contains("ClOrdID", acceptedHtml);
        Assert.Contains("OrderID", acceptedHtml);
        Assert.Contains("ExecID", acceptedHtml);
        Assert.Contains("não indica uma execução no mercado", acceptedHtml);
        string acceptedId = ResultValue(acceptedHtml, "ClOrdID");
        Assert.False(string.IsNullOrWhiteSpace(acceptedId));
        Assert.Equal(HiddenValue(form, "SubmissionToken"), HiddenValue(acceptedHtml, "SubmissionToken"));
        Assert.Equal(99_998_000.01m, harness.Fix.Processor.GetExposure("PETR4"));

        using HttpResponseMessage repeated = await PostAsync(client, form, "PETR4", "Buy", "99999", "999.99");
        string repeatedHtml = WebUtility.HtmlDecode(await repeated.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Contains("Este formulário já foi enviado", repeatedHtml);
        Assert.DoesNotContain("capacidade global de envio está esgotada", repeatedHtml);
        Assert.DoesNotContain("Ordem aceita", repeatedHtml);
        Assert.Equal(99_998_000.01m, harness.Fix.Processor.GetExposure("PETR4"));

        string newForm = await client.GetStringAsync("/");
        Assert.NotEqual(HiddenValue(form, "SubmissionToken"), HiddenValue(newForm, "SubmissionToken"));
        using HttpResponseMessage rejected = await PostAsync(client, newForm, "PETR4", "Buy", "3", "999.99");
        string rejectedHtml = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Null(rejected.Headers.Location);
        Assert.Contains("Ordem rejeitada", rejectedHtml);
        Assert.Contains("ClOrdID", rejectedHtml);
        Assert.Contains("Rejected", rejectedHtml);
        Assert.NotEqual(acceptedId, ResultValue(rejectedHtml, "ClOrdID"));
        Assert.DoesNotContain(acceptedId, rejectedHtml);
        Assert.Equal(99_998_000.01m, harness.Fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task SameBrowser_ConcurrentPosts_ResponsesRemainIsolatedWhenCompletedInReverseOrder()
    {
        var fake = new ControlledSubmissionService();
        await using var harness = await WebHarness.StartAsync(submissionService: fake);
        using HttpClient client = harness.CreateClient();
        string firstForm = await client.GetStringAsync("/");
        string secondForm = await client.GetStringAsync("/");
        Assert.NotEqual(HiddenValue(firstForm, "SubmissionToken"), HiddenValue(secondForm, "SubmissionToken"));

        Task<HttpResponseMessage> firstPost = PostAsync(client, firstForm, "PETR4", "Buy", "1", "10.00");
        ControlledSubmissionService.Request first = await fake.Requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage> secondPost = PostAsync(client, secondForm, "VALE3", "Sell", "2", "20.00");
        ControlledSubmissionService.Request second = await fake.Requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("PETR4", first.Submission.Symbol);
        Assert.Equal("VALE3", second.Submission.Symbol);
        Assert.False(firstPost.IsCompleted);
        Assert.False(secondPost.IsCompleted);

        using HttpResponseMessage replay = await PostAsync(client, firstForm, "PETR4", "Buy", "1", "10.00")
            .WaitAsync(TimeSpan.FromSeconds(10));
        string replayHtml = WebUtility.HtmlDecode(await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("Este formulário já foi enviado", replayHtml);
        Assert.DoesNotContain("capacidade global de envio está esgotada", replayHtml);
        Assert.Equal(2, fake.CallCount);

        second.Completion.SetResult(new(SubmissionStatus.Rejected, "second-id", "second-rejection",
            OrderId: "second-order", ExecId: "second-exec", RejectionReason: 0));
        using HttpResponseMessage secondResponse = await secondPost.WaitAsync(TimeSpan.FromSeconds(10));
        string secondHtml = WebUtility.HtmlDecode(await secondResponse.Content.ReadAsStringAsync());
        Assert.False(firstPost.IsCompleted);
        first.Completion.SetResult(new(SubmissionStatus.Accepted, "first-id", "first-accepted",
            OrderId: "first-order", ExecId: "first-exec"));
        using HttpResponseMessage firstResponse = await firstPost.WaitAsync(TimeSpan.FromSeconds(10));
        string firstHtml = WebUtility.HtmlDecode(await firstResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Null(firstResponse.Headers.Location);
        Assert.Null(secondResponse.Headers.Location);
        Assert.Contains("Ordem aceita", firstHtml);
        Assert.Contains("first-accepted", firstHtml);
        Assert.Equal("first-id", ResultValue(firstHtml, "ClOrdID"));
        Assert.DoesNotContain("second-id", firstHtml);
        Assert.DoesNotContain("second-rejection", firstHtml);
        Assert.DoesNotContain("Ordem rejeitada", firstHtml);
        Assert.Contains("Ordem rejeitada", secondHtml);
        Assert.Contains("second-rejection", secondHtml);
        Assert.Equal("second-id", ResultValue(secondHtml, "ClOrdID"));
        Assert.DoesNotContain("first-id", secondHtml);
        Assert.DoesNotContain("first-accepted", secondHtml);
        Assert.DoesNotContain("Ordem aceita", secondHtml);
        Assert.Equal(HiddenValue(firstForm, "SubmissionToken"), HiddenValue(firstHtml, "SubmissionToken"));
        Assert.Equal(HiddenValue(secondForm, "SubmissionToken"), HiddenValue(secondHtml, "SubmissionToken"));

        using HttpResponseMessage replayAfterCompletion = await PostAsync(client, firstForm, "PETR4", "Buy", "1", "10.00");
        Assert.Contains("Este formulário já foi enviado", WebUtility.HtmlDecode(await replayAfterCompletion.Content.ReadAsStringAsync()));
        Assert.Equal(2, fake.CallCount);
    }

    [Theory]
    [InlineData(14, null)]
    [InlineData(151, null)]
    [InlineData(6, null)]
    [InlineData(14, "1")]
    [InlineData(151, "100")]
    [InlineData(6, "10.00")]
    [InlineData(14, "not-a-number")]
    [InlineData(38, "101")]
    public async Task InitiatorCallback_InconsistentRejectedReport_IsTechnicalFailureWithUnknownOutcome(int tag, string? value)
    {
        await using var harness = await WebHarness.StartAsync();
        var pending = harness.Factory.Services.GetRequiredService<PendingOrderRegistry>();
        var application = harness.Factory.Services.GetRequiredService<OrderGenerator.Infrastructure.Fix.FixApplication>();
        var order = new OutboundOrder("callback-rejected", "PETR4", OrderSide.Buy, 100, 10m);
        Assert.True(pending.TryRegister(order, 10, out var request, out _));
        Assert.NotNull(request);
        Assert.True(pending.TryBeginSend(request));
        var report = RejectedReport(order);
        if (value is null)
            report.RemoveField(tag);
        else
            report.SetField(new StringField(tag, value));

        application.OnMessage(report, new SessionID("FIX.4.4", harness.Fix.InitiatorId, harness.Fix.AcceptorId));
        SubmissionResult result = await request.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(SubmissionStatus.TechnicalFailure, result.Status);
        Assert.True(result.OutcomeMayBeUnknown);
        Assert.Equal(order.ClOrdId, result.ClOrdId);
        Assert.False(pending.TryGet(order.ClOrdId, out _));
    }

    [Fact]
    public async Task InitiatorCallback_ValidRejectedReport_RemainsBusinessRejection()
    {
        await using var harness = await WebHarness.StartAsync();
        var pending = harness.Factory.Services.GetRequiredService<PendingOrderRegistry>();
        var application = harness.Factory.Services.GetRequiredService<OrderGenerator.Infrastructure.Fix.FixApplication>();
        var order = new OutboundOrder("callback-valid", "PETR4", OrderSide.Buy, 100, 10m);
        Assert.True(pending.TryRegister(order, 10, out var request, out _));
        Assert.NotNull(request);
        Assert.True(pending.TryBeginSend(request));

        application.OnMessage(RejectedReport(order), new SessionID("FIX.4.4", harness.Fix.InitiatorId, harness.Fix.AcceptorId));
        SubmissionResult result = await request.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(SubmissionStatus.Rejected, result.Status);
        Assert.False(result.OutcomeMayBeUnknown);
        Assert.Equal(order.ClOrdId, result.ClOrdId);
        Assert.Equal("exposure limit", result.Explanation);
        Assert.Equal(0, result.RejectionReason);
        Assert.False(pending.TryGet(order.ClOrdId, out _));
    }

    private static QuickFix.FIX44.ExecutionReport RejectedReport(OutboundOrder order)
    {
        var report = new QuickFix.FIX44.ExecutionReport();
        report.SetField(new StringField(11, order.ClOrdId));
        report.SetField(new StringField(37, "order-id"));
        report.SetField(new StringField(17, "exec-id"));
        report.SetField(new StringField(150, "8"));
        report.SetField(new StringField(39, "8"));
        report.SetField(new StringField(55, order.Symbol));
        report.SetField(new StringField(54, "1"));
        report.SetField(new StringField(38, "100"));
        report.SetField(new StringField(44, "10.00"));
        report.SetField(new StringField(60, "20260101-12:00:00"));
        report.SetField(new StringField(14, "0"));
        report.SetField(new StringField(151, "0"));
        report.SetField(new StringField(6, "0"));
        report.SetField(new StringField(103, "0"));
        report.SetField(new StringField(58, "exposure limit"));
        return report;
    }

    [Fact]
    public async Task AcceptorDisconnect_RealWebSubmission_ShowsUnavailableRatherThanBusinessRejection()
    {
        await using var harness = await WebHarness.StartAsync();
        using HttpClient client = harness.CreateClient();
        string form = await client.GetStringAsync("/");
        await harness.Fix.StopAcceptorAsync();
        var sessionId = new SessionID("FIX.4.4", harness.Fix.InitiatorId, harness.Fix.AcceptorId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Session.LookupSession(sessionId)?.IsLoggedOn == true)
            await Task.Delay(50, timeout.Token);

        using HttpResponseMessage response = await PostAsync(client, form, "PETR4", "Buy", "1", "10.00");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Serviço indisponível", html);
        Assert.DoesNotContain("Ordem rejeitada", html);
        Assert.Equal(0m, harness.Fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task ConcurrentSubmissions_RealGeneratorAndAcceptor_CorrelateAndRemovePendingOrders()
    {
        await using var harness = await WebHarness.StartAsync();
        var service = harness.Factory.Services.GetRequiredService<IOrderSubmissionService>();
        var pending = harness.Factory.Services.GetRequiredService<PendingOrderRegistry>();
        Task<SubmissionResult>[] submissions = Enumerable.Range(1, 20)
            .Select(quantity => service.SubmitAsync(new OrderSubmission("VALE3", OrderSide.Buy, quantity, 0.10m)))
            .ToArray();

        SubmissionResult[] results = await Task.WhenAll(submissions).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.All(results, result =>
        {
            Assert.Equal(SubmissionStatus.Accepted, result.Status);
            Assert.False(string.IsNullOrEmpty(result.ClOrdId));
            Assert.False(string.IsNullOrEmpty(result.OrderId));
            Assert.False(string.IsNullOrEmpty(result.ExecId));
            Assert.False(pending.TryGet(result.ClOrdId!, out _));
        });
        Assert.Equal(20, results.Select(result => result.ClOrdId).Distinct().Count());
        Assert.Equal(20, results.Select(result => result.OrderId).Distinct().Count());
        Assert.Equal(21.00m, harness.Fix.Processor.GetExposure("VALE3"));
    }

    [Fact]
    public async Task ResponseTimeout_LateRealFixReport_DoesNotBecomeRejectionOrTriggerResubmission()
    {
        var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        int calls = 0;
        var processor = new OrderProcessor(stage =>
        {
            if (stage == OrderProcessingStage.BeforeValidation)
            {
                Interlocked.Increment(ref calls);
                processingStarted.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Test did not release the acceptor.");
            }
        });
        await using var harness = await WebHarness.StartAsync(processor, responseTimeoutSeconds: 1);
        try
        {
            var service = harness.Factory.Services.GetRequiredService<IOrderSubmissionService>();
            var pending = harness.Factory.Services.GetRequiredService<PendingOrderRegistry>();
            var session = Session.LookupSession(new SessionID("FIX.4.4", harness.Fix.InitiatorId, harness.Fix.AcceptorId));
            Assert.NotNull(session);
            ulong nextIncoming = session.NextTargetMsgSeqNum;
            Task<SubmissionResult> submission = service.SubmitAsync(new OrderSubmission("PETR4", OrderSide.Buy, 1, 10m));
            await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            SubmissionResult result = await submission.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(SubmissionStatus.TimedOut, result.Status);
            Assert.True(result.OutcomeMayBeUnknown);
            Assert.False(pending.TryGet(result.ClOrdId!, out _));
            release.Set();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (session.NextTargetMsgSeqNum <= nextIncoming)
                await Task.Delay(50, timeout.Token);
            Assert.Equal(10m, processor.GetExposure("PETR4"));
            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.False(pending.TryGet(result.ClOrdId!, out _));
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task PendingRealOrder_Disconnect_CompletesAsUnknownWithoutAutomaticResubmission()
    {
        var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicationPrepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        int calls = 0;
        var processor = new OrderProcessor(stage =>
        {
            if (stage == OrderProcessingStage.BeforeValidation)
            {
                Interlocked.Increment(ref calls);
                processingStarted.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(20)))
                    throw new TimeoutException("Test did not release the acceptor.");
            }
            else if (stage == OrderProcessingStage.BeforePublication)
            {
                publicationPrepared.TrySetResult();
            }
        });
        await using var harness = await WebHarness.StartAsync(processor, responseTimeoutSeconds: 20);
        try
        {
            var service = harness.Factory.Services.GetRequiredService<IOrderSubmissionService>();
            var pending = harness.Factory.Services.GetRequiredService<PendingOrderRegistry>();
            var session = Session.LookupSession(new SessionID("FIX.4.4", harness.Fix.InitiatorId, harness.Fix.AcceptorId));
            Assert.NotNull(session);
            Task<SubmissionResult> submission = service.SubmitAsync(new OrderSubmission("PETR4", OrderSide.Buy, 1, 10m));
            await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            session.Disconnect("Controlled test disconnect while order is pending");
            SubmissionResult result = await submission.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(SubmissionStatus.Disconnected, result.Status);
            Assert.True(result.OutcomeMayBeUnknown);
            Assert.False(string.IsNullOrEmpty(result.ClOrdId));
            Assert.False(pending.TryGet(result.ClOrdId!, out _));

            release.Set();
            await publicationPrepared.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(10m, processor.GetExposure("PETR4"));
            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.False(pending.TryGet(result.ClOrdId!, out _));
        }
        finally
        {
            release.Set();
        }
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string html,
        string symbol, string side, string quantity, string price)
    {
        string antiforgery = HiddenValue(html, "__RequestVerificationToken");
        string submission = HiddenValue(html, "SubmissionToken");
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["SubmissionToken"] = submission,
            ["Symbol"] = symbol, ["Side"] = side, ["Quantity"] = quantity, ["Price"] = price
        });
        return await client.PostAsync("/", body);
    }

    private static string HiddenValue(string html, string name)
    {
        Match input = Regex.Match(html, $@"<input\b[^>]*\bname=""{Regex.Escape(name)}""[^>]*>",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        Assert.True(input.Success, $"Missing hidden input {name}.");
        Match value = Regex.Match(input.Value, @"\bvalue=""([^""]*)""",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        Assert.True(value.Success, $"Missing value for {name}.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static string ResultValue(string html, string name)
    {
        Match match = Regex.Match(html, $@"<dt>{Regex.Escape(name)}</dt>\s*<dd>([^<]+)</dd>",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, $"Missing result {name}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class ControlledSubmissionService : IOrderSubmissionService
    {
        public sealed record Request(OrderSubmission Submission, TaskCompletionSource<SubmissionResult> Completion);
        public Channel<Request> Requests { get; } = Channel.CreateUnbounded<Request>();
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public Task<SubmissionResult> SubmitAsync(OrderSubmission submission, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            var completion = new TaskCompletionSource<SubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.True(Requests.Writer.TryWrite(new Request(submission, completion)));
            return completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class WebHarness : IAsyncDisposable
    {
        private WebHarness(FixSession fix, GeneratorFactory factory) { Fix = fix; Factory = factory; }
        public FixSession Fix { get; }
        public GeneratorFactory Factory { get; }

        public static async Task<WebHarness> StartAsync(OrderProcessor? processor = null, int responseTimeoutSeconds = 5,
            IOrderSubmissionService? submissionService = null)
        {
            FixSession fix = await FixSession.StartAsync(startPeer: false, processor: processor);
            var factory = new GeneratorFactory(fix, responseTimeoutSeconds, submissionService);
            var harness = new WebHarness(fix, factory);
            try
            {
                using HttpClient client = harness.CreateClient();
                await client.GetAsync("/");
                var sessionId = new SessionID("FIX.4.4", fix.InitiatorId, fix.AcceptorId);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (Session.LookupSession(sessionId)?.IsLoggedOn != true)
                    await Task.Delay(50, timeout.Token);
                return harness;
            }
            catch
            {
                await harness.DisposeAsync();
                throw;
            }
        }

        public HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await Fix.DisposeAsync();
        }
    }

    private sealed class GeneratorFactory(FixSession fix, int responseTimeoutSeconds, IOrderSubmissionService? submissionService) : WebApplicationFactory<generator::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FixInitiator:SenderCompId"] = fix.InitiatorId,
                    ["FixInitiator:TargetCompId"] = fix.AcceptorId,
                    ["FixInitiator:SocketConnectHost"] = "127.0.0.1",
                    ["FixInitiator:SocketConnectPort"] = fix.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["FixInitiator:HeartBtInt"] = "30",
                    ["FixInitiator:ReconnectIntervalSeconds"] = "1",
                    ["FixInitiator:StartTime"] = "00:00:00",
                    ["FixInitiator:EndTime"] = "00:00:00",
                    ["FixInitiator:RuntimePath"] = Path.Combine(fix.Root, "generator"),
                    ["FixInitiator:ResponseTimeoutSeconds"] = responseTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["FixInitiator:MaxPendingOrders"] = "100"
                }));
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                if (submissionService is not null)
                {
                    services.RemoveAll<IOrderSubmissionService>();
                    services.AddSingleton(submissionService);
                }
            });
        }
    }
}
