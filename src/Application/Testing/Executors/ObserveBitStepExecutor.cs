using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ObserveBitStepExecutor : IStepExecutor<ObserveBitStep>
{
    public Type StepType => typeof(ObserveBitStep);

    public async Task<StepOutcome> ExecuteAsync(ObserveBitStep step, ITestRunnerContext context, CancellationToken ct)
    {
        try
        {
            var match = await context.Signals
                .Where(v => v.MessageName == step.Message && v.SignalName == step.Signal
                         && ((v.RawValue != 0) == step.Expected))
                .Timeout(step.Within)
                .FirstAsync()
                .ToTask(ct);

            return StepOutcome.Passed;
        }
        catch (TimeoutException) { return StepOutcome.Failed; }
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ObserveBitStep)step, context, ct);
}
