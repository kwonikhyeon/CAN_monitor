using CanMonitor.Core.Abstractions;
using CanMonitor.Infrastructure.Can.CandleLight;
using CanMonitor.Infrastructure.Can.Slcan;
using CanMonitor.Infrastructure.Can.Virtual;

namespace CanMonitor.Wpf.Infrastructure;

public sealed class CanBusFactory : ICanBusFactory
{
    public IReadOnlyList<AdapterOption> Known { get; } = new[]
    {
        new AdapterOption(AdapterKind.Virtual, "Virtual"),
        new AdapterOption(AdapterKind.CandleLightUsb, "CandleLight USB"),
        new AdapterOption(AdapterKind.SlcanSerial, "SLCAN Serial")
    };

    public ICanBus Create(AdapterKind kind) => kind switch
    {
        AdapterKind.Virtual => new VirtualCanBus(),
        AdapterKind.CandleLightUsb => new CandleLightCanBus(),
        AdapterKind.SlcanSerial => new SlcanSerialCanBus(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported adapter")
    };
}
