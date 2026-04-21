using System.Collections.Concurrent;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class RawFrameStore
{
    public const int DefaultCapacity = 10_000;

    private readonly ConcurrentQueue<CanFrame> _queue = new();
    private readonly int _capacity;
    private long _dropped;

    public RawFrameStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public void Record(CanFrame frame)
    {
        _queue.Enqueue(frame);
        while (_queue.Count > _capacity && _queue.TryDequeue(out _))
            Interlocked.Increment(ref _dropped);
    }

    public IReadOnlyCollection<CanFrame> Snapshot() => _queue.ToArray();
}
