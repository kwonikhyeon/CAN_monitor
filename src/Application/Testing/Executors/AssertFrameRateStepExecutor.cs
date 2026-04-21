using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class AssertFrameRateStepExecutor : IStepExecutor<AssertFrameRateStep>
{
    public Type StepType => typeof(AssertFrameRateStep);

    public async Task<StepOutcome> ExecuteAsync(AssertFrameRateStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var window = TimeSpan.FromSeconds(1);
        var count = await context.Bus.Frames
            .Where(f => f.Id == step.CanId)
            .Buffer(window)
            .Take(1)
            .Select(b => b.Count)
            .FirstAsync()
            .ToTask(ct);

        var measured = count / window.TotalSeconds;
        var tolerance = step.HzExpected * (step.TolerancePct / 100.0);
        return Math.Abs(measured - step.HzExpected) <= tolerance
            ? StepOutcome.Passed
            : StepOutcome.Failed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((AssertFrameRateStep)step, context, ct);
}
