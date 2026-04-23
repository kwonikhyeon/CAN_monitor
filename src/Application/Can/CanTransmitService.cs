using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanTransmitService
{
    private readonly ICanBus _bus;

    public CanTransmitService(ICanBus bus)
    {
        _bus = bus;
    }

    public Task SendAsync(CanFrame frame, CancellationToken ct = default)
        => _bus.SendAsync(frame, ct);
}
