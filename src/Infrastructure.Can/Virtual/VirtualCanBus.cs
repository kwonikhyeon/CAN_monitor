using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Infrastructure.Can.Virtual;

/// <summary>
/// In-process loopback bus. Tests (and Phase 2/4 Simulator) inject frames
/// via <see cref="Inject"/>; <see cref="CanBusBase.SendAsync"/> echoes them
/// to subscribers with Direction=Tx automatically.
/// </summary>
public sealed class VirtualCanBus : CanBusBase
{
    public override string Name => "Virtual";

    public void Inject(CanFrame frame) => PublishRx(frame);

    protected override Task OpenDriverAsync(CanBusOptions options, CancellationToken ct)
        => Task.CompletedTask;

    protected override Task StopDriverAsync() => Task.CompletedTask;

    protected override Task SendDriverAsync(CanFrame frame, CancellationToken ct)
        => Task.CompletedTask;
}
