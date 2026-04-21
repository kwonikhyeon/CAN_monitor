using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ObserveSignalStepExecutor : IStepExecutor<ObserveSignalStep>
{
    public Type StepType => typeof(ObserveSignalStep);

    public async Task<StepOutcome> ExecuteAsync(ObserveSignalStep step, ITestRunnerContext context, CancellationToken ct)
    {
        try
        {
            var match = await context.Signals
                .Where(v => v.MessageName == step.Message && v.SignalName == step.Signal)
                .Timeout(step.Within)
                .FirstAsync()
                .ToTask(ct);

            return Math.Abs(match.PhysicalValue - step.Expected) <= step.Tolerance
                ? StepOutcome.Passed
                : StepOutcome.Failed;
        }
        catch (TimeoutException) { return StepOutcome.Failed; }
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ObserveSignalStep)step, context, ct);
}
