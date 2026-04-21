using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Channels;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class TxScheduler : ITxScheduler, IAsyncDisposable
{
    private readonly ICanBus _bus;
    private readonly IScheduler _scheduler;
    private readonly Channel<TxJob> _channel;
    private readonly Task _workerTask;
    private readonly CancellationTokenSource _cts = new();
    private long _enqueuedCount;
    private long _processedCount;

    public TxScheduler(ICanBus bus, IScheduler? scheduler = null)
    {
        _bus = bus;
        _scheduler = scheduler ?? DefaultScheduler.Instance;
        _channel = Channel.CreateBounded<TxJob>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public IDisposable Schedule(string name, Func<CanFrame> factory, TimeSpan period)
    {
        return Observable.Interval(period, _scheduler)
            .Subscribe(_ => Enqueue(new TxJob(name, factory())));
    }

    public IDisposable SendBurst(IReadOnlyList<CanFrame> frames, TimeSpan? interval = null)
    {
        if (!interval.HasValue || interval.Value == TimeSpan.Zero)
        {
            foreach (var frame in frames)
                Enqueue(new TxJob("Burst", frame));
            return System.Reactive.Disposables.Disposable.Empty;
        }

        return Observable.Generate(
                0, i => i < frames.Count, i => i + 1, i => frames[i], _ => interval.Value, _scheduler)
            .Subscribe(frame => Enqueue(new TxJob("Burst", frame)));
    }

    private void Enqueue(TxJob job)
    {
        if (_channel.Writer.TryWrite(job))
            Interlocked.Increment(ref _enqueuedCount);
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(ct))
            {
                try { await _bus.SendAsync(job.Frame, ct); }
                catch (OperationCanceledException) { break; }
                catch { /* Phase 2a: swallow; Plan B에서 ILogger 주입 */ }
                finally { Interlocked.Increment(ref _processedCount); }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// 테스트 전용 hook — 지금까지 enqueue된 모든 작업이 worker에서 처리되었음을 결정적으로 보장.
    /// 타이밍 sleep 없음. Phase 2a 테스트 시나리오는 채널 bound(1024) 이하로 enqueue하므로
    /// DropOldest로 인한 카운터 divergence는 발생하지 않는다.
    /// </summary>
    public async Task DrainForTestsAsync()
    {
        var target = Interlocked.Read(ref _enqueuedCount);
        while (Interlocked.Read(ref _processedCount) < target)
            await Task.Yield();
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try { await _workerTask; } catch { }
        _cts.Dispose();
    }
}
