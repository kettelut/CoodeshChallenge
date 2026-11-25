using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderGenerator.Application;
using OrderGenerator.Infrastructure.Fix;
using Xunit;

namespace OrderProcessing.Integration.Tests;

public sealed class FixInitiatorReconnectTests
{
    [Fact]
    public async Task Generator_ConnectionClosedBeforeLogon_RetriesAtConfiguredInterval()
    {
        const int reconnectSeconds = 3;
        string root = Path.Combine(Path.GetTempPath(), "order-generator-retry-tests", Guid.NewGuid().ToString("N"));
        string suffix = Guid.NewGuid().ToString("N");
        string sender = "I" + suffix;
        string target = "A" + suffix;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var registry = new PendingOrderRegistry();
        var application = new FixApplication(registry, NullLogger<FixApplication>.Instance);
        var options = Options.Create(new FixInitiatorOptions
        {
            SenderCompId = sender,
            TargetCompId = target,
            SocketConnectHost = IPAddress.Loopback.ToString(),
            SocketConnectPort = port,
            HeartBtInt = 30,
            ReconnectIntervalSeconds = reconnectSeconds,
            StartTime = "00:00:00",
            EndTime = "00:00:00",
            RuntimePath = root
        });
        var submissionOptions = Options.Create(new OrderSubmissionOptions
        {
            ResponseTimeoutSeconds = 10,
            MaxPendingOrders = 10
        });
        var service = new FixInitiatorService(application, registry, options, submissionOptions,
            new TestEnvironment(root), NullLogger<FixInitiatorService>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));

        try
        {
            await service.StartAsync(deadline.Token);
            var attempts = new List<long>();
            for (int i = 0; i < 3; i++)
            {
                using TcpClient connection = await listener.AcceptTcpClientAsync(deadline.Token);
                attempts.Add(Stopwatch.GetTimestamp());
                string logon = await ReadLogonAsync(connection, sender, target, deadline.Token);
                Assert.Contains("\u000135=A\u0001", logon);
                Assert.Contains($"\u000149={sender}\u0001", logon);
                Assert.Contains($"\u000156={target}\u0001", logon);
                // Closing our accepted connection forces a new attempt without a FIX Logon response.
            }

            for (int i = 1; i < attempts.Count; i++)
            {
                TimeSpan elapsed = Stopwatch.GetElapsedTime(attempts[i - 1], attempts[i]);
                Assert.True(elapsed >= TimeSpan.FromSeconds(1.5) && elapsed <= TimeSpan.FromSeconds(9),
                    $"Retry {i}: expected approximately {reconnectSeconds}s, observed {elapsed}.");
            }
        }
        finally
        {
            try { await service.StopAsync(CancellationToken.None); }
            finally
            {
                try { service.Dispose(); }
                finally
                {
                    listener.Stop();
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                }
            }
        }
    }

    private static async Task<string> ReadLogonAsync(TcpClient connection, string sender, string target,
        CancellationToken cancellationToken)
    {
        using NetworkStream stream = connection.GetStream();
        var text = new StringBuilder();
        byte[] buffer = new byte[4096];
        while (text.Length < 16_384)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
            string current = text.ToString();
            if (current.Contains("\u000135=A\u0001", StringComparison.Ordinal)
                && current.Contains($"\u000149={sender}\u0001", StringComparison.Ordinal)
                && current.Contains($"\u000156={target}\u0001", StringComparison.Ordinal))
                return current;
        }
        return text.ToString();
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OrderProcessing.Integration.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
