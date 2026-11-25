using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderAccumulator.Infrastructure.Fix;
using OrderProcessing.Domain;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using Xunit;

namespace OrderProcessing.Integration.Tests;

// Each test owns both engines, their session identifiers and their on-disk stores.
internal sealed class FixSession : IAsyncDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "order-fix-tests", Guid.NewGuid().ToString("N"));
    private readonly TestPeer _peer = new();
    private FixAcceptorService? _acceptor;
    private SocketInitiator? _initiator;
    private SessionID? _sessionId;
    private FixAcceptorOptions? _acceptorOptions;
    private SessionSettings? _initiatorSettings;

    private FixSession(OrderProcessor? processor) => Processor = processor ?? new OrderProcessor();

    public OrderProcessor Processor { get; }
    public int Port { get; private set; }
    public string AcceptorId { get; private set; } = "";
    public string InitiatorId { get; private set; } = "";
    public string Root => _root;

    public static async Task<FixSession> StartAsync(bool startPeer = true, OrderProcessor? processor = null)
    {
        var fixture = new FixSession(processor);
        try
        {
            await fixture.StartEnginesAsync(startPeer);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private async Task StartEnginesAsync(bool startPeer)
    {
        Directory.CreateDirectory(_root);
        // The socket reserves an OS-assigned ephemeral loopback port until configuration is ready.
        // QuickFIX/n must bind it itself, so there is a small unavoidable release/bind race.
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        string suffix = Guid.NewGuid().ToString("N");
        string acceptorId = "A" + suffix;
        string initiatorId = "I" + suffix;
        Port = port;
        AcceptorId = acceptorId;
        InitiatorId = initiatorId;
        _acceptorOptions = new FixAcceptorOptions
        {
            SenderCompId = acceptorId, TargetCompId = initiatorId,
            SocketAcceptHost = "127.0.0.1", SocketAcceptPort = port, HeartBtInt = 30,
            StartTime = "00:00:00", EndTime = "00:00:00",
            RuntimePath = Path.Combine(_root, "acceptor")
        };
        _acceptor = CreateAcceptor();

        string store = Path.Combine(_root, "initiator", "store");
        string logs = Path.Combine(_root, "initiator", "logs");
        Directory.CreateDirectory(store);
        Directory.CreateDirectory(logs);
        var settings = new SessionSettings();
        var defaults = new SettingsDictionary("DEFAULT");
        defaults.SetString("FileLogPath", logs);
        defaults.SetLong("ReconnectInterval", 1);
        settings.Set(defaults);
        var session = new SettingsDictionary();
        session.SetString("ConnectionType", "initiator");
        session.SetString("SocketConnectHost", "127.0.0.1");
        session.SetLong("SocketConnectPort", port);
        session.SetLong("HeartBtInt", 30);
        session.SetString("StartTime", "00:00:00");
        session.SetString("EndTime", "00:00:00");
        session.SetString("FileStorePath", store);
        session.SetString("FileLogPath", logs);
        session.SetString("UseDataDictionary", "Y");
        session.SetString("DataDictionary", Path.Combine(AppContext.BaseDirectory, "FIX44.xml"));
        session.SetString("ResetOnLogon", "N");
        session.SetString("ResetOnLogout", "N");
        session.SetString("ResetOnDisconnect", "N");
        _sessionId = new SessionID("FIX.4.4", initiatorId, acceptorId);
        settings.Set(_sessionId, session);
        _initiatorSettings = settings;
        if (startPeer)
            _initiator = CreateInitiator();

        reservation.Stop();
        await _acceptor.StartAsync(CancellationToken.None);
        if (_initiator is not null)
        {
            _initiator.Start();
            await _peer.LoggedOn.Task.WaitAsync(Deadline);
        }
    }

    public async Task<Message> SendAsync(string id, string side = "1", string quantity = "100",
        string? price = "10.00", string symbol = "PETR4")
    {
        var order = new QuickFix.FIX44.NewOrderSingle(
            new ClOrdID(id), new Symbol(symbol), new Side(side[0]),
            new TransactTime(DateTime.UtcNow), new OrdType(OrdType.LIMIT));
        // StringField is intentional: typed decimal fields would round or overflow before transport.
        order.SetField(new StringField(Tags.OrderQty, quantity));
        if (price is not null)
            order.SetField(new StringField(Tags.Price, price));
        Assert.True(Session.SendToTarget(order, _sessionId!));
        var sent = await _peer.Sent.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
        Assert.Equal(quantity, sent.GetString(Tags.OrderQty));
        Assert.Equal(price, sent.IsSetField(Tags.Price) ? sent.GetString(Tags.Price) : null);
        return await _peer.Received.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
    }

    public async Task StopAcceptorAsync()
    {
        if (_acceptor is not null)
        {
            await _acceptor.StopAsync(CancellationToken.None);
            _acceptor.Dispose();
            _acceptor = null;
        }
    }

    public async Task RestartAcceptorAsync()
    {
        Assert.NotNull(_initiator);
        await PauseAcceptorAsync();
        await _peer.LoggedOut.Task.WaitAsync(Deadline);
        // Restart the same service, without collecting the dictionary reader.
        await ResumeAcceptorAsync();
        await _peer.Reconnected.Task.WaitAsync(Deadline);
    }

    public async Task PauseAcceptorAsync()
    {
        Assert.NotNull(_acceptor);
        await _acceptor.StopAsync(CancellationToken.None);
    }

    public async Task ResumeAcceptorAsync()
    {
        Assert.NotNull(_acceptor);
        await _acceptor.StartAsync(CancellationToken.None);
    }

    private FixAcceptorService CreateAcceptor() => new(
        new FixApplication(Processor, NullLogger<FixApplication>.Instance),
        Options.Create(_acceptorOptions!), new TestEnvironment(_root), NullLogger<FixAcceptorService>.Instance);

    private SocketInitiator CreateInitiator() => new(_peer, new FileStoreFactory(_initiatorSettings!),
        _initiatorSettings!, new FileLogFactory(_initiatorSettings!), new QuickFix.FIX44.MessageFactory());

    public async Task RestartInitiatorAsync()
    {
        Assert.NotNull(_initiator);
        _initiator.Stop();
        _initiator.Dispose();
        _initiator = null;
        await _peer.LoggedOut.Task.WaitAsync(Deadline);
        _initiator = CreateInitiator();
        _initiator.Start();
        try
        {
            await _peer.Reconnected.Task.WaitAsync(Deadline);
        }
        catch (TimeoutException exception)
        {
            string[] logs = Directory.GetFiles(_root, "*.event.current.log", SearchOption.AllDirectories)
                .Select(path =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    return Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path))) + ": " + reader.ReadToEnd();
                }).ToArray();
            throw new TimeoutException("FIX reconnection timed out. " + string.Join(Environment.NewLine, logs), exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _initiator?.Stop();
            _initiator?.Dispose();
            await StopAcceptorAsync();
        }
        finally
        {
            // QuickFIX/n's dictionary loader can leave an XML reader awaiting finalization
            // after Stop/Dispose. Release those handles before removing the isolated directory.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestPeer : IApplication
    {
        public TaskCompletionSource LoggedOn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LoggedOut { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Reconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<Message> Received { get; } = Channel.CreateUnbounded<Message>();
        public Channel<Message> Sent { get; } = Channel.CreateUnbounded<Message>();
        public void OnCreate(SessionID sessionID) { }
        public void OnLogon(SessionID sessionID)
        {
            if (!LoggedOn.TrySetResult())
                Reconnected.TrySetResult();
        }
        public void OnLogout(SessionID sessionID) => LoggedOut.TrySetResult();
        public void ToAdmin(Message message, SessionID sessionID) { }
        public void FromAdmin(Message message, SessionID sessionID)
        {
            if (message.Header.GetString(Tags.MsgType) == "3")
                Received.Writer.TryWrite(message);
        }
        public void ToApp(Message message, SessionID sessionID) => Sent.Writer.TryWrite(message);
        public void FromApp(Message message, SessionID sessionID) => Received.Writer.TryWrite(message);
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OrderProcessing.Integration.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
