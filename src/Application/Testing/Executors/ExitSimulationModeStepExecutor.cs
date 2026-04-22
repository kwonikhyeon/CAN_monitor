using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ExitSimulationModeStepExecutor : IStepExecutor<ExitSimulationModeStep>
{
    private readonly IVirtualInputService _service;
    private readonly IBusHeartbeatProvider[] _providers;

    public ExitSimulationModeStepExecutor(IVirtualInputService service, IEnumerable<IBusHeartbeatProvider> providers)
    {
        _service = service;
        _providers = providers.ToArray();
    }

    public Type StepType => typeof(ExitSimulationModeStep);

    public async Task<StepOutcome> ExecuteAsync(ExitSimulationModeStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var vi = _providers.FirstOrDefault(p => p.Name == "VirtualInput");
        vi?.SetEnabled(false);
        await _service.ExitSimulationModeAsync(ct);
        return StepOutcome.Passed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ExitSimulationModeStep)step, context, ct);
}
