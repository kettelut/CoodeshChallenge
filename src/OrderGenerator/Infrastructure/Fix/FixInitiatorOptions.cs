namespace OrderGenerator.Infrastructure.Fix;

public sealed class FixInitiatorOptions
{
    public string SenderCompId { get; set; } = "";
    public string TargetCompId { get; set; } = "";
    public string SocketConnectHost { get; set; } = "";
    public int SocketConnectPort { get; set; }
    public int HeartBtInt { get; set; }
    public int ReconnectIntervalSeconds { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string RuntimePath { get; set; } = "";
}
