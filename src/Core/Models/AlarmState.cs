namespace CanMonitor.Core.Models;

public sealed record AlarmState(
    string Code,
    AlarmSeverity Severity,
    string Message,
    bool Active,
    DateTimeOffset Since);
