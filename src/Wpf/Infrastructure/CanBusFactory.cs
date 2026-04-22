using CanMonitor.Core.Abstractions;
using CanMonitor.Infrastructure.Can.Virtual;

namespace CanMonitor.Wpf.Infrastructure;

public sealed class CanBusFactory : ICanBusFactory
{
    public IReadOnlyList<AdapterOption> Known { get; } = new[]
    {
        new AdapterOption(AdapterKind.Virtual, "Virtual")
    };

    public ICanBus Create(AdapterKind kind) => kind switch
    {
        AdapterKind.Virtual => new VirtualCanBus(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported adapter")
    };
}
