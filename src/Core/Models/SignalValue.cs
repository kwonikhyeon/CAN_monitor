namespace CanMonitor.Core.Models;

public sealed record SignalValue(
    string MessageName,
    string SignalName,
    double RawValue,
    double PhysicalValue,
    string? Unit,
    DateTimeOffset Timestamp);
