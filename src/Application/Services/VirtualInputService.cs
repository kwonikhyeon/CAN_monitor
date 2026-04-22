using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Services;

public sealed class VirtualInputService : IVirtualInputService, IDisposable
{
    private readonly BehaviorSubject<VirtualInputState> _state = new(new VirtualInputState());
    private int _simulationActive;

    public bool IsSimulationModeActive => Volatile.Read(ref _simulationActive) != 0;
    public VirtualInputState Current => _state.Value;
    public IObservable<VirtualInputState> Changes => _state.AsObservable();

    public Task EnterSimulationModeAsync(CancellationToken ct = default)
    {
        Interlocked.Exchange(ref _simulationActive, 1);
        return Task.CompletedTask;
    }

    public Task ExitSimulationModeAsync(CancellationToken ct = default)
    {
        Interlocked.Exchange(ref _simulationActive, 0);
        return Task.CompletedTask;
    }

    public void Update(VirtualInputState next) => _state.OnNext(next);

    public void Dispose()
    {
        _state.OnCompleted();
        _state.Dispose();
    }
}
