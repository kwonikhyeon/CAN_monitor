using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ManualConfirmStepExecutor : IStepExecutor<ManualConfirmStep>
{
    public Type StepType => typeof(ManualConfirmStep);

    public Task<StepOutcome> ExecuteAsync(ManualConfirmStep step, ITestRunnerContext context, CancellationToken ct)
        => Task.FromResult(StepOutcome.ManualRequired);

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ManualConfirmStep)step, context, ct);
}
