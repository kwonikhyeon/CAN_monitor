using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface IAlarmEngine
{
    IObservable<AlarmState> AlarmChanges { get; }
    IReadOnlyCollection<AlarmState> CurrentAlarms { get; }
    void Submit(SignalValue value);
}
