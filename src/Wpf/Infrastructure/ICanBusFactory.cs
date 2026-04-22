using CanMonitor.Core.Abstractions;

namespace CanMonitor.Wpf.Infrastructure;

public interface ICanBusFactory
{
    IReadOnlyList<AdapterOption> Known { get; }
    ICanBus Create(AdapterKind kind);
}
