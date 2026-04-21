namespace CanMonitor.Core.Testing;

public sealed record TestCase(
    string Id,
    string Category,
    string Name,
    IReadOnlyList<TestStep> Prerequisites,
    IReadOnlyList<TestStep> Steps,
    string? FailCode = null);
