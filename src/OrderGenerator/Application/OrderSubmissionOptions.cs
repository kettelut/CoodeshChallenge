namespace OrderGenerator.Application;

public sealed class OrderSubmissionOptions
{
    public int ResponseTimeoutSeconds { get; set; }
    public int MaxPendingOrders { get; set; }
}
