using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public interface IAlarmRule
{
    string Code { get; }
    AlarmState? Evaluate(SignalValue value, AlarmState? current);
}
