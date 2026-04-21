using CanMonitor.Core.Abstractions;
using CanMonitor.Infrastructure.Can.Virtual;

namespace CanMonitor.Infrastructure.Can;

public sealed class CanBusFactory : ICanBusFactory
{
    public IReadOnlyList<string> AvailableAdapters { get; } = new[] { "Virtual" };

    public ICanBus Create(string adapterName) => adapterName switch
    {
        "Virtual" => new VirtualCanBus(),
        _         => throw new ArgumentException(
            $"Unknown adapter '{adapterName}'. Available: {string.Join(", ", AvailableAdapters)}",
            nameof(adapterName))
    };
}
