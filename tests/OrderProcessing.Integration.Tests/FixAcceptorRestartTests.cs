using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderGenerator.Application;
using OrderGenerator.Infrastructure.Fix;
using OrderProcessing.Domain;
using QuickFix;
using QuickFix.Fields;
using Xunit;

namespace OrderProcessing.Integration.Tests;

// Other fixtures force collection during teardown. Do not let them do so between
// the acceptor's stop and restart, where a dictionary handle may still be open.
[CollectionDefinition("Acceptor restart", DisableParallelization = true)]
public sealed class AcceptorRestartCollection;

[Collection("Acceptor restart")]
public sealed class FixAcceptorRestartTests
{
    [Fact]
    public async Task AcceptorRestart_SameStoresAndProcessor_ReconnectsAndReplaysWithoutDuplicateExposure()
    {
        await using var fix = await FixSession.StartAsync();
        Message original = await fix.SendAsync("before-acceptor-restart", quantity: "2", price: "10.00");
        Assert.Equal("0", original.GetString(Tags.ExecType));
        Assert.Equal(20m, fix.Processor.GetExposure("PETR4"));

        string dictionary = Assert.Single(Directory.GetFiles(Path.Combine(fix.Root, "acceptor"), "FIX44-exact-*.xml"));
        byte[] dictionaryContent = await File.ReadAllBytesAsync(dictionary);
        DateTime dictionaryWriteTime = File.GetLastWriteTimeUtc(dictionary);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(fix.Root, "initiator", "store"), "*.seqnums"));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(fix.Root, "acceptor", "store"), "*.seqnums"));
        var initiatorId = new SessionID("FIX.4.4", fix.InitiatorId, fix.AcceptorId);
        var acceptorId = new SessionID("FIX.4.4", fix.AcceptorId, fix.InitiatorId);
        Session initiator = Assert.IsType<Session>(Session.LookupSession(initiatorId));
        Session acceptor = Assert.IsType<Session>(Session.LookupSession(acceptorId));
        ulong initiatorSent = initiator.NextSenderMsgSeqNum;
        ulong initiatorReceived = initiator.NextTargetMsgSeqNum;
        ulong acceptorSent = acceptor.NextSenderMsgSeqNum;
        ulong acceptorReceived = acceptor.NextTargetMsgSeqNum;
        Assert.True(initiatorSent > 1 && initiatorReceived > 1 && acceptorSent > 1 && acceptorReceived > 1);

        await fix.RestartAcceptorAsync();
        Assert.Equal(dictionary, Assert.Single(Directory.GetFiles(Path.Combine(fix.Root, "acceptor"), "FIX44-exact-*.xml")));
        Assert.Equal(dictionaryContent, await File.ReadAllBytesAsync(dictionary));
        Assert.Equal(dictionaryWriteTime, File.GetLastWriteTimeUtc(dictionary));

        // Check before new business messages can mask a reset of the stores.
        initiator = Assert.IsType<Session>(Session.LookupSession(initiatorId));
        acceptor = Assert.IsType<Session>(Session.LookupSession(acceptorId));
        Assert.True(initiator.IsLoggedOn && acceptor.IsLoggedOn);
        Assert.True(initiator.NextSenderMsgSeqNum > initiatorSent);
        Assert.True(initiator.NextTargetMsgSeqNum > initiatorReceived);
        Assert.True(acceptor.NextSenderMsgSeqNum > acceptorSent);
        Assert.True(acceptor.NextTargetMsgSeqNum > acceptorReceived);

        Message replay = await fix.SendAsync("before-acceptor-restart", quantity: "2", price: "10.000");
        Assert.Equal("before-acceptor-restart", replay.GetString(Tags.ClOrdID));
        Assert.Equal("0", replay.GetString(Tags.ExecType));
        Assert.Equal(original.GetString(Tags.OrderID), replay.GetString(Tags.OrderID));
        Assert.Equal(20m, fix.Processor.GetExposure("PETR4"));

        Message after = await fix.SendAsync("after-acceptor-restart", quantity: "3", price: "10.00");
        Assert.Equal("after-acceptor-restart", after.GetString(Tags.ClOrdID));
        Assert.Equal("0", after.GetString(Tags.ExecType));
        Assert.Equal("0", after.GetString(Tags.OrdStatus));
        Assert.Equal(0m, after.GetDecimal(Tags.CumQty));
        Assert.Equal(3m, after.GetDecimal(Tags.LeavesQty));
        Assert.Equal(50m, fix.Processor.GetExposure("PETR4"));

    }

    [Fact]
    public async Task RealGenerator_AfterAcceptorRestart_LogsOnAgainAndSubmitsAnotherOrder()
    {
        await using var fix = await FixSession.StartAsync(startPeer: false);
        var registry = new PendingOrderRegistry();
        var signals = new SessionSignals();
        var application = new FixApplication(registry, signals);
        var submissionOptions = Options.Create(new OrderSubmissionOptions
        {
            ResponseTimeoutSeconds = 10,
            MaxPendingOrders = 10
        });
        var initiatorOptions = Options.Create(new FixInitiatorOptions
        {
            SenderCompId = fix.InitiatorId,
            TargetCompId = fix.AcceptorId,
            SocketConnectHost = "127.0.0.1",
            SocketConnectPort = fix.Port,
            HeartBtInt = 30,
            ReconnectIntervalSeconds = 1,
            StartTime = "00:00:00",
            EndTime = "00:00:00",
            RuntimePath = Path.Combine(fix.Root, "generator")
        });
        var initiator = new FixInitiatorService(application, registry, initiatorOptions, submissionOptions,
            new TestEnvironment(fix.Root), NullLogger<FixInitiatorService>.Instance);
        var orders = new OrderSubmissionService(initiator, registry, submissionOptions,
            NullLogger<OrderSubmissionService>.Instance);
        TimeSpan deadline = TimeSpan.FromSeconds(15);
        try
        {
            await initiator.StartAsync(CancellationToken.None);
            await signals.FirstLogon.Task.WaitAsync(deadline);
            SubmissionResult before = await orders.SubmitAsync(new("PETR4", OrderSide.Buy, 2, 10m)).WaitAsync(deadline);
            Assert.Equal(SubmissionStatus.Accepted, before.Status);
            Assert.Equal(20m, fix.Processor.GetExposure("PETR4"));

            await fix.PauseAcceptorAsync();
            await signals.Logout.Task.WaitAsync(deadline);
            await fix.ResumeAcceptorAsync();
            await signals.SecondLogon.Task.WaitAsync(deadline);

            SubmissionResult after = await orders.SubmitAsync(new("PETR4", OrderSide.Buy, 3, 10m)).WaitAsync(deadline);
            Assert.Equal(SubmissionStatus.Accepted, after.Status);
            Assert.NotEqual(before.ClOrdId, after.ClOrdId);
            Assert.NotEqual(before.OrderId, after.OrderId);
            Assert.Equal(50m, fix.Processor.GetExposure("PETR4"));
            Assert.False(registry.TryGet(after.ClOrdId!, out _));
        }
        finally
        {
            try { await initiator.StopAsync(CancellationToken.None); }
            finally { initiator.Dispose(); }
        }
    }

    // FixApplication publishes these lifecycle logs after updating the real registry.
    private sealed class SessionSignals : ILogger<FixApplication>
    {
        private int _logons;
        public TaskCompletionSource FirstLogon { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondLogon { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Logout { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            if (message.StartsWith("FIX session logged on ", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _logons) == 1) FirstLogon.TrySetResult();
                else SecondLogon.TrySetResult();
            }
            else if (message.StartsWith("FIX session logged out or disconnected ", StringComparison.Ordinal))
                Logout.TrySetResult();
        }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OrderProcessing.Integration.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
