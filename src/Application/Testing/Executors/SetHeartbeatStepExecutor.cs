using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class SetHeartbeatStepExecutor : IStepExecutor<SetHeartbeatStep>
{
    private readonly IBusHeartbeatProvider[] _providers;

    public SetHeartbeatStepExecutor(IEnumerable<IBusHeartbeatProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public Type StepType => typeof(SetHeartbeatStep);

    public Task<StepOutcome> ExecuteAsync(SetHeartbeatStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var provider = _providers.FirstOrDefault(p => p.Name == step.Name);
        if (provider is null) return Task.FromResult(StepOutcome.Failed);
        provider.SetEnabled(step.Enabled);
        return Task.FromResult(StepOutcome.Passed);
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((SetHeartbeatStep)step, context, ct);
}
