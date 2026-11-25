namespace OrderAccumulator.Infrastructure.Fix;

public sealed class FixAcceptorOptions
{
    public string SenderCompId { get; set; } = "";
    public string TargetCompId { get; set; } = "";
    public string SocketAcceptHost { get; set; } = "";
    public int SocketAcceptPort { get; set; }
    public int HeartBtInt { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string RuntimePath { get; set; } = "";
}
