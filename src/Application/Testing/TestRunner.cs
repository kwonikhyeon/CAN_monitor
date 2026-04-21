using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing;

public sealed class TestRunner : ITestRunner
{
    private readonly IReadOnlyDictionary<Type, IStepExecutor> _executors;
    private readonly ITestRunnerContext _context;

    public TestRunner(IEnumerable<IStepExecutor> executors, ITestRunnerContext context)
    {
        _executors = executors.ToDictionary(e => e.StepType);
        _context = context;
    }

    public async Task<TestResult> RunAsync(
        TestCase testCase,
        IProgress<TestStepProgress>? progress = null,
        CancellationToken ct = default)
    {
        var log = new List<TestStepProgress>();
        var startedAt = DateTimeOffset.UtcNow;
        var overall = TestOutcome.Passed;
        string? reason = null;

        var steps = testCase.Prerequisites.Concat(testCase.Steps).ToArray();
        for (int i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            progress?.Report(new TestStepProgress(i, step, StepOutcome.Running, null, DateTimeOffset.UtcNow));

            if (!_executors.TryGetValue(step.GetType(), out var executor))
                throw new NotSupportedException(
                    $"Step type {step.GetType().Name} is not registered in this runner build.");

            StepOutcome outcome;
            string? message = null;
            try
            {
                outcome = await executor.ExecuteAsync(step, _context, ct);
            }
            catch (Exception ex)
            {
                outcome = StepOutcome.Failed;
                message = ex.Message;
            }

            var entry = new TestStepProgress(i, step, outcome, message, DateTimeOffset.UtcNow);
            log.Add(entry);
            progress?.Report(entry);

            if (outcome == StepOutcome.Failed) { overall = TestOutcome.Failed; reason = message ?? "step failed"; break; }
            if (outcome == StepOutcome.ManualRequired) { overall = TestOutcome.ManualRequired; reason = "manual confirmation required"; break; }
        }

        return new TestResult(testCase.Id, overall, reason, log, startedAt, DateTimeOffset.UtcNow);
    }
}
