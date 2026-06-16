namespace YourCustomAgentHarness.Api;

using System.Collections.Concurrent;
using System.Threading.Channels;
using YourCustomAgentHarness.Shared.Telemetry;

public sealed class EventBus
{
    private readonly int _capacity;
    private readonly Queue<ActivityEvent> _buffer = new();
    private readonly ConcurrentBag<Channel<ActivityEvent>> _subs = new();
    private readonly object _gate = new();

    public EventBus(int capacity) { _capacity = capacity; }

    public IReadOnlyList<ActivityEvent> Snapshot()
    {
        lock (_gate) return _buffer.ToArray();
    }

    public void Publish(ActivityEvent ev)
    {
        lock (_gate)
        {
            _buffer.Enqueue(ev);
            while (_buffer.Count > _capacity) _buffer.Dequeue();
        }
        foreach (var ch in _subs)
        {
            ch.Writer.TryWrite(ev);
        }
    }

    public Subscription Subscribe(CancellationToken ct)
    {
        var ch = Channel.CreateUnbounded<ActivityEvent>();
        _subs.Add(ch);
        ct.Register(() => ch.Writer.TryComplete());
        return new Subscription(ch);
    }

    public sealed class Subscription : IDisposable
    {
        private readonly Channel<ActivityEvent> _ch;
        public ChannelReader<ActivityEvent> Reader => _ch.Reader;
        public Subscription(Channel<ActivityEvent> ch) { _ch = ch; }
        public void Dispose() => _ch.Writer.TryComplete();
    }
}
