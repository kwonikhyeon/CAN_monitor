using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface IVirtualInputService
{
    bool IsSimulationModeActive { get; }
    VirtualInputState Current { get; }
    IObservable<VirtualInputState> Changes { get; }

    Task EnterSimulationModeAsync(CancellationToken ct = default);
    Task ExitSimulationModeAsync(CancellationToken ct = default);
    void Update(VirtualInputState next);
}
