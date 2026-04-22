using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class EnterSimulationModeStepExecutor : IStepExecutor<EnterSimulationModeStep>
{
    private readonly IVirtualInputService _service;
    private readonly IBusHeartbeatProvider[] _providers;

    public EnterSimulationModeStepExecutor(IVirtualInputService service, IEnumerable<IBusHeartbeatProvider> providers)
    {
        _service = service;
        _providers = providers.ToArray();
    }

    public Type StepType => typeof(EnterSimulationModeStep);

    public async Task<StepOutcome> ExecuteAsync(EnterSimulationModeStep step, ITestRunnerContext context, CancellationToken ct)
    {
        await _service.EnterSimulationModeAsync(ct);
        var vi = _providers.FirstOrDefault(p => p.Name == "VirtualInput");
        vi?.SetEnabled(true);
        return StepOutcome.Passed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((EnterSimulationModeStep)step, context, ct);
}
