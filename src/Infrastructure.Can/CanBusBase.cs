using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Infrastructure.Can;

/// <summary>
/// Shared template for concrete <see cref="ICanBus"/> implementations. Owns
/// the synchronized frame subject, serialized send lock, and the close sequence.
/// Derived classes supply driver-specific open/stop/send logic and publish
/// received frames via <see cref="PublishRx"/> — the template ensures the
/// payload is copied into an independently-owned buffer (§8).
/// </summary>
public abstract class CanBusBase : ICanBus
{
    private readonly Subject<CanFrame> _inner;
    private readonly ISubject<CanFrame> _frames;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _isOpen;
    private volatile bool _isClosed;
    private readonly object _closeGate = new();

    protected CanBusBase()
    {
        _inner  = new Subject<CanFrame>();
        _frames = Subject.Synchronize(_inner);
    }

    public abstract string Name { get; }
    public bool IsOpen => _isOpen;
    public IObservable<CanFrame> Frames => _inner;            // subscribers see synchronized delivery

    public async Task OpenAsync(CanBusOptions options, CancellationToken ct = default)
    {
        if (_isOpen) throw new InvalidOperationException($"{Name} already open");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        await OpenDriverAsync(options, linked.Token).ConfigureAwait(false);
        _isOpen = true;
    }

    public async Task CloseAsync()
    {
        lock (_closeGate)
        {
            if (_isClosed) return;
            _isClosed = true;
        }

        _cts.Cancel();
        try   { await StopDriverAsync().ConfigureAwait(false); }
        catch { /* swallow — close must complete */ }
        _frames.OnCompleted();
        _isOpen = false;
    }

    public async Task SendAsync(CanFrame frame, CancellationToken ct = default)
    {
        if (!_isOpen) throw new InvalidOperationException($"{Name} not open");
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SendDriverAsync(frame, ct).ConfigureAwait(false);
            var echo = frame with { Direction = CanDirection.Tx, Data = frame.Data.ToArray() };
            _frames.OnNext(echo);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Derived classes call this to publish an incoming frame. Payload is copied.</summary>
    protected void PublishRx(CanFrame frame)
    {
        var owned = frame.Data.ToArray();
        _frames.OnNext(frame with { Data = owned, Direction = CanDirection.Rx });
    }

    protected abstract Task OpenDriverAsync(CanBusOptions options, CancellationToken ct);
    protected abstract Task StopDriverAsync();
    protected abstract Task SendDriverAsync(CanFrame frame, CancellationToken ct);

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _inner.Dispose();
        _sendLock.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
