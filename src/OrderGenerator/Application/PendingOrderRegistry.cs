namespace OrderGenerator.Application;

// The lock coordinates availability, send-start and completion. No network I/O
// occurs while it is held. A send-start is conservatively considered uncertain.
public sealed class PendingOrderRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PendingOrder> _pending = new(StringComparer.Ordinal);
    private bool _connected;
    private bool _stopped;

    public sealed class PendingOrder(OutboundOrder order)
    {
        public OutboundOrder Order { get; } = order;
        public TaskCompletionSource<SubmissionResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool SendStarted { get; set; }
    }

    public void Connected()
    {
        lock (_gate) { if (!_stopped) _connected = true; }
    }

    public void Disconnected(bool stopping = false)
    {
        lock (_gate)
        {
            _stopped |= stopping;
            _connected = false;
            foreach (PendingOrder pending in _pending.Values)
            {
                pending.Completion.TrySetResult(new(SubmissionStatus.Disconnected, pending.Order.ClOrdId,
                    "FIX session disconnected; reconcile any order whose outcome is unknown.",
                    pending.SendStarted));
            }
            _pending.Clear();
        }
    }

    public bool TryRegister(OutboundOrder order, int maximum, out PendingOrder? pending, out string error)
    {
        lock (_gate)
        {
            pending = null;
            if (!_connected) { error = "FIX session is not logged on."; return false; }
            if (_pending.Count >= maximum) { error = "Pending order capacity reached."; return false; }
            pending = new PendingOrder(order);
            if (!_pending.TryAdd(order.ClOrdId, pending))
            {
                pending = null;
                error = "Order identifier already pending.";
                return false;
            }
            error = "";
            return true;
        }
    }

    public bool TryBeginSend(PendingOrder pending)
    {
        lock (_gate)
        {
            if (!_connected || !_pending.TryGetValue(pending.Order.ClOrdId, out var current)
                || !ReferenceEquals(current, pending)) return false;
            pending.SendStarted = true;
            return true;
        }
    }

    public bool TryGet(string id, out PendingOrder? pending)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(id, out pending) && pending.SendStarted) return true;
            pending = null;
            return false;
        }
    }

    public bool Complete(PendingOrder pending, SubmissionResult result)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(pending.Order.ClOrdId, out var current)
                || !ReferenceEquals(current, pending)) return false;
            _pending.Remove(pending.Order.ClOrdId);
            return pending.Completion.TrySetResult(result);
        }
    }

    public void CompleteInterrupted(PendingOrder pending, SubmissionStatus status, string explanation)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(pending.Order.ClOrdId, out var current)
                || !ReferenceEquals(current, pending)) return;
            _pending.Remove(pending.Order.ClOrdId);
            pending.Completion.TrySetResult(new(status, pending.Order.ClOrdId, explanation, pending.SendStarted));
        }
    }
}
