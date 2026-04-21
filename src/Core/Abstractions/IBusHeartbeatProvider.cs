using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

/// <summary>
/// Periodic sender abstraction. Enabled state is read-only; mutation flows through
/// <see cref="SetEnabled"/> so that <see cref="EnabledChanges"/> emits a single source
/// of truth for UI and BusLifecycleService alike. See spec §6/§12.
/// </summary>
public interface IBusHeartbeatProvider
{
    string Name { get; }
    TimeSpan Period { get; }
    CanFrame BuildFrame();

    bool Enabled { get; }
    IObservable<bool> EnabledChanges { get; }
    void SetEnabled(bool enabled);
}
