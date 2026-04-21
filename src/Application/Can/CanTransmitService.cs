using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanTransmitService
{
    private readonly ICanBus _bus;
    private readonly ITxScheduler _scheduler;

    public CanTransmitService(ICanBus bus, ITxScheduler scheduler)
    {
        _bus = bus;
        _scheduler = scheduler;
    }

    public Task SendAsync(CanFrame frame, CancellationToken ct = default)
        => _bus.SendAsync(frame, ct);

    public IDisposable SchedulePeriodic(string name, Func<CanFrame> factory, TimeSpan period)
        => _scheduler.Schedule(name, factory, period);

    public IDisposable Burst(IReadOnlyList<CanFrame> frames, TimeSpan? interval = null)
        => _scheduler.SendBurst(frames, interval);
}
