using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class SendCanFrameStepExecutor : IStepExecutor<SendCanFrameStep>
{
    public Type StepType => typeof(SendCanFrameStep);

    public async Task<StepOutcome> ExecuteAsync(SendCanFrameStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var frame = new CanFrame(step.Id, step.IsExtended, step.Data, DateTimeOffset.UtcNow, CanDirection.Tx);
        await context.Bus.SendAsync(frame, ct);
        return StepOutcome.Passed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((SendCanFrameStep)step, context, ct);
}
