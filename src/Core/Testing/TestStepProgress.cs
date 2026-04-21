namespace CanMonitor.Core.Testing;

public enum StepOutcome
{
    Pending = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    ManualRequired = 4
}

public sealed record TestStepProgress(
    int StepIndex,
    TestStep Step,
    StepOutcome Outcome,
    string? Message,
    DateTimeOffset At);
