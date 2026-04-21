using CanMonitor.Core.Testing;

namespace CanMonitor.Core.Abstractions;

public interface ITestRunner
{
    Task<TestResult> RunAsync(
        TestCase testCase,
        IProgress<TestStepProgress>? progress = null,
        CancellationToken ct = default);
}
