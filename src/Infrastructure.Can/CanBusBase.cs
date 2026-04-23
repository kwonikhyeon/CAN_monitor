using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Infrastructure.Can;

/// <summary>
/// 구체적인 <see cref="ICanBus"/> 구현을 위한 공통 템플릿. 동기화된 프레임
/// Subject, 직렬화된 송신 락, 그리고 종료 시퀀스를 소유한다. 파생 클래스는
/// 드라이버별 open/stop/send 로직을 제공하고 수신된 프레임을
/// <see cref="PublishRx"/> 를 통해 publish 한다 — 템플릿은 payload 가
/// 독립 소유 버퍼로 복사되도록 보장한다 (§8).
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
    public IObservable<CanFrame> Frames => _inner;            // 구독자는 동기화된 배달을 본다

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
        catch { /* close 는 반드시 완료되어야 하므로 예외 흡수 */ }
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

    /// <summary>파생 클래스가 수신 프레임을 publish 할 때 호출. Payload 는 복사된다.</summary>
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
