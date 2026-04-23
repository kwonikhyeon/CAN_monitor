using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface IAlarmRule
{
    string Code { get; }
    AlarmState? Evaluate(SignalValue value, AlarmState? current);
}
