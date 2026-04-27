using CanMonitor.Infrastructure.Can.Virtual;
using CanMonitor.Infrastructure.Can.CandleLight;
using CanMonitor.Infrastructure.Can.Slcan;
using CanMonitor.Wpf.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Infrastructure;

public class CanBusFactoryTests
{
    [Fact]
    public void Known_contains_virtual_adapter()
    {
        var factory = new CanBusFactory();
        factory.Known.Select(a => a.Kind).Should().Contain(new[]
        {
            AdapterKind.Virtual,
            AdapterKind.CandleLightUsb,
            AdapterKind.SlcanSerial
        });
    }

    [Fact]
    public void Create_virtual_returns_VirtualCanBus()
    {
        var factory = new CanBusFactory();
        var bus = factory.Create(AdapterKind.Virtual);
        bus.Should().BeOfType<VirtualCanBus>();
    }

    [Fact]
    public void Create_candle_light_returns_CandleLightCanBus()
    {
        var factory = new CanBusFactory();
        var bus = factory.Create(AdapterKind.CandleLightUsb);
        bus.Should().BeOfType<CandleLightCanBus>();
    }

    [Fact]
    public void Create_slcan_serial_returns_SlcanSerialCanBus()
    {
        var factory = new CanBusFactory();
        var bus = factory.Create(AdapterKind.SlcanSerial);
        bus.Should().BeOfType<SlcanSerialCanBus>();
    }

    [Fact]
    public void Create_unsupported_throws()
    {
        var factory = new CanBusFactory();
        var act = () => factory.Create((AdapterKind)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
