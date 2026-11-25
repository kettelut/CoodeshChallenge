using System.Globalization;
using Microsoft.Extensions.Options;
using OrderGenerator.Application;
using OrderProcessing.Domain;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Store;
using QuickFix.Transport;

namespace OrderGenerator.Infrastructure.Fix;

public sealed class FixInitiatorService(
    FixApplication application,
    PendingOrderRegistry registry,
    IOptions<FixInitiatorOptions> options,
    IOptions<OrderSubmissionOptions> submissionOptions,
    IHostEnvironment environment,
    ILogger<FixInitiatorService> logger) : IHostedService, IOrderTransport, IDisposable
{
    private SocketInitiator? _initiator;
    private SessionID? _sessionId;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FixInitiatorOptions config = options.Value;
        Validate(config, submissionOptions.Value);
        string runtime = Path.GetFullPath(config.RuntimePath, environment.ContentRootPath);
        string store = Path.Combine(runtime, "store");
        string logs = Path.Combine(runtime, "logs");
        Directory.CreateDirectory(store);
        Directory.CreateDirectory(logs);
        var settings = new SessionSettings();
        var defaults = new SettingsDictionary("DEFAULT");
        defaults.SetString("FileLogPath", logs);
        // SocketInitiator reads the reconnect interval from global defaults, not the session.
        defaults.SetLong(SessionSettings.RECONNECT_INTERVAL, config.ReconnectIntervalSeconds);
        settings.Set(defaults);
        var session = new SettingsDictionary();
        session.SetString("ConnectionType", "initiator");
        session.SetString("SocketConnectHost", config.SocketConnectHost);
        session.SetLong("SocketConnectPort", config.SocketConnectPort);
        session.SetLong("HeartBtInt", config.HeartBtInt);
        session.SetString("StartTime", config.StartTime);
        session.SetString("EndTime", config.EndTime);
        session.SetString("FileStorePath", store);
        session.SetString("FileLogPath", logs);
        session.SetString("UseDataDictionary", "Y");
        session.SetString("DataDictionary", Path.Combine(AppContext.BaseDirectory, "FIX44.xml"));
        session.SetString("ValidateFieldsHaveValues", "Y");
        session.SetString("ValidateFieldsOutOfOrder", "Y");
        session.SetString("ResetOnLogon", "N");
        session.SetString("ResetOnLogout", "N");
        session.SetString("ResetOnDisconnect", "N");
        session.SetString("RedactFieldsInLogs", "553,554,925");
        _sessionId = new SessionID("FIX.4.4", config.SenderCompId, config.TargetCompId);
        settings.Set(_sessionId, session);
        application.ConfigureSession(_sessionId);

        // The FIX store persists messages for sequence recovery. Engine payload
        // logging is disabled to avoid writing complete financial messages/secrets.
        _initiator = new SocketInitiator(application, new FileStoreFactory(settings), settings,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, new QuickFix.FIX44.MessageFactory());
        try { _initiator.Start(); }
        catch
        {
            _initiator.Dispose();
            _initiator = null;
            throw;
        }
        logger.LogInformation("FIX initiator started for {SessionId}", _sessionId);
        return Task.CompletedTask;
    }

    public bool Send(OutboundOrder order)
    {
        var session = _sessionId ?? throw new InvalidOperationException("FIX initiator not started.");
        var message = new QuickFix.FIX44.NewOrderSingle(
            new ClOrdID(order.ClOrdId),
            new Symbol(order.Symbol),
            new Side(order.Side == OrderSide.Buy ? Side.BUY : Side.SELL),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT));
        message.SetField(new OrderQty(order.Quantity));
        message.SetField(new Price(order.Price));
        bool sent = Session.SendToTarget(message, session);
        logger.LogInformation("FIX NewOrderSingle send attempted {SessionId} {ClOrdId} {Sent}", session, order.ClOrdId, sent);
        return sent;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        registry.Disconnected(stopping: true);
        if (_initiator is not null)
        {
            _initiator.Stop();
            logger.LogInformation("FIX initiator stopped");
        }
        return Task.CompletedTask;
    }

    public void Dispose() => _initiator?.Dispose();

    private static void Validate(FixInitiatorOptions config, OrderSubmissionOptions submission)
    {
        if (string.IsNullOrWhiteSpace(config.SenderCompId) ||
            string.IsNullOrWhiteSpace(config.TargetCompId) ||
            config.SenderCompId.Any(char.IsControl) || config.TargetCompId.Any(char.IsControl) ||
            config.SenderCompId == config.TargetCompId || string.IsNullOrWhiteSpace(config.SocketConnectHost) ||
            config.SocketConnectPort is < 1 or > 65535 || config.HeartBtInt <= 0 ||
            config.ReconnectIntervalSeconds <= 0 || submission.ResponseTimeoutSeconds is < 1 or > 86400 ||
            submission.MaxPendingOrders <= 0 || string.IsNullOrWhiteSpace(config.RuntimePath) ||
            !TimeOnly.TryParseExact(config.StartTime, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !TimeOnly.TryParseExact(config.EndTime, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidOperationException("Invalid FIX initiator configuration.");
    }
}
