using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuickFix;
using QuickFix.Store;

namespace OrderAccumulator.Infrastructure.Fix;

public sealed class FixAcceptorService(
    FixApplication application,
    IOptions<FixAcceptorOptions> options,
    IHostEnvironment environment,
    ILogger<FixAcceptorService> logger) : IHostedService, IDisposable
{
    private ThreadedSocketAcceptor? _acceptor;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_acceptor is not null)
        {
            throw new InvalidOperationException("FIX acceptor is already started.");
        }

        FixAcceptorOptions config = options.Value;
        Validate(config);

        string runtime = Path.GetFullPath(config.RuntimePath, environment.ContentRootPath);
        string store = Path.Combine(runtime, "store");
        string logs = Path.Combine(runtime, "logs");
        Directory.CreateDirectory(store);
        Directory.CreateDirectory(logs);
        string dictionary = CreateExactNumberDictionary(runtime);

        var settings = new SessionSettings();
        var defaults = new SettingsDictionary("DEFAULT");
        defaults.SetString("FileLogPath", logs);
        settings.Set(defaults);
        var session = new SettingsDictionary();
        session.SetString("ConnectionType", "acceptor");
        session.SetString("SocketAcceptHost", config.SocketAcceptHost);
        session.SetLong("SocketAcceptPort", config.SocketAcceptPort);
        session.SetLong("HeartBtInt", config.HeartBtInt);
        session.SetString("StartTime", config.StartTime);
        session.SetString("EndTime", config.EndTime);
        session.SetString("FileStorePath", store);
        session.SetString("FileLogPath", logs);
        session.SetString("UseDataDictionary", "Y");
        session.SetString("DataDictionary", dictionary);
        session.SetString("ValidateFieldsHaveValues", "Y");
        session.SetString("ValidateFieldsOutOfOrder", "Y");
        session.SetString("ResetOnLogon", "N");
        session.SetString("ResetOnLogout", "N");
        session.SetString("ResetOnDisconnect", "N");
        session.SetString("RedactFieldsInLogs", "553,554,925");
        var sessionId = new SessionID("FIX.4.4", config.SenderCompId, config.TargetCompId);
        settings.Set(sessionId, session);

        // The FIX store persists messages for sequence recovery; disable engine
        // logging to avoid writing complete financial messages to log files.
        _acceptor = new ThreadedSocketAcceptor(application, new FileStoreFactory(settings),
            settings, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, new QuickFix.FIX44.MessageFactory());
        try
        {
            _acceptor.Start();
        }
        catch
        {
            _acceptor.Dispose();
            _acceptor = null;
            throw;
        }

        logger.LogInformation("FIX acceptor started for {SessionId} on {Host}:{Port}",
            sessionId, config.SocketAcceptHost, config.SocketAcceptPort);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_acceptor is not null)
        {
            try
            {
                _acceptor.Stop();
            }
            finally
            {
                _acceptor.Dispose();
                _acceptor = null;
            }

            logger.LogInformation("FIX acceptor stopped");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _acceptor?.Dispose();
        _acceptor = null;
    }

    private static void Validate(FixAcceptorOptions config)
    {
        if (string.IsNullOrWhiteSpace(config.SenderCompId) ||
            string.IsNullOrWhiteSpace(config.TargetCompId) ||
            config.SenderCompId.Any(char.IsControl) ||
            config.TargetCompId.Any(char.IsControl) ||
            config.SenderCompId == config.TargetCompId ||
            string.IsNullOrWhiteSpace(config.SocketAcceptHost) ||
            config.SocketAcceptPort is < 1 or > 65535 ||
            config.HeartBtInt <= 0 ||
            !TimeOnly.TryParseExact(config.StartTime, "HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _) ||
            !TimeOnly.TryParseExact(config.EndTime, "HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _) ||
            string.IsNullOrWhiteSpace(config.RuntimePath))
        {
            throw new InvalidOperationException("Invalid FIX acceptor configuration.");
        }
    }

    private static string CreateExactNumberDictionary(string runtime)
    {
        // QuickFIX/n 1.14.1 validates QTY/PRICE through decimal.Parse before FromApp.
        // Keep all official FIX44 definitions, except these two fields are validated
        // as exact numeric text by FixApplication, without decimal overflow/rounding.
        string source = Path.Combine(AppContext.BaseDirectory, "FIX44.xml");
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        XDocument document = XDocument.Load(reader);
        XElement fields = document.Root?.Element("fields")
            ?? throw new InvalidOperationException("FIX44 dictionary has no fields.");
        foreach (string tag in new[] { "38", "44" })
        {
            XElement field = fields.Elements("field").Single(element => (string?)element.Attribute("number") == tag);
            field.SetAttributeValue("type", "STRING");
        }

        using var buffer = new MemoryStream();
        document.Save(buffer);
        byte[] content = buffer.ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(content));
        string path = Path.Combine(runtime, $"FIX44-exact-{hash}.xml");

        // QuickFIX/n 1.14.1 may leave its dictionary file stream open until GC.
        // Publish immutable, content-addressed files instead of rewriting one in use.
        if (!File.Exists(path))
        {
            string temporaryPath = Path.Combine(runtime, $".FIX44-exact-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(content);
                }

                try
                {
                    File.Move(temporaryPath, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Another acceptor published the same dictionary concurrently.
                }
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
        {
            throw new InvalidOperationException("Existing FIX dictionary does not match its expected content.");
        }

        return path;
    }
}
