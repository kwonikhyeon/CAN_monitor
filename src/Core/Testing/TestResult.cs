namespace CanMonitor.Core.Testing;

public enum TestOutcome
{
    Passed = 0,
    Failed = 1,
    ManualRequired = 2,
    NotSupported = 3
}

public sealed record TestResult(
    string TestCaseId,
    TestOutcome Outcome,
    string? Reason,
    IReadOnlyList<TestStepProgress> StepLog,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
