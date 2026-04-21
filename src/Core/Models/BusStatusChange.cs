namespace CanMonitor.Core.Models;

public sealed record BusStatusChange(
    BusStatus Status,
    string? Message,
    Exception? Error,
    int RetryAttempt,
    DateTimeOffset At);
