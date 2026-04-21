using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class WaitStepExecutor : IStepExecutor<WaitStep>
{
    public Type StepType => typeof(WaitStep);

    public async Task<StepOutcome> ExecuteAsync(WaitStep step, ITestRunnerContext context, CancellationToken ct)
    {
        await Task.Delay(step.Duration, ct);
        return StepOutcome.Passed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((WaitStep)step, context, ct);
}
