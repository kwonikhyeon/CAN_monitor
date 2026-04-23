using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public sealed class BitAlarmRule : IAlarmRule
{
    private readonly AlarmSeverity _severity;
    private readonly string _messageName;
    private readonly string _signalName;
    private readonly string _description;

    public BitAlarmRule(string code, AlarmSeverity severity,
        string messageName, string signalName, string description)
    {
        Code = code;
        _severity = severity;
        _messageName = messageName;
        _signalName = signalName;
        _description = description;
    }

    public string Code { get; }

    public AlarmState? Evaluate(SignalValue value, AlarmState? prior)
    {
        if (value.MessageName != _messageName || value.SignalName != _signalName)
            return null;

        var active = value.RawValue == 1;
        if (prior is not null && prior.Active == active)
            return null;

        return new AlarmState(Code, _severity, _description, active, value.Timestamp);
    }
}
