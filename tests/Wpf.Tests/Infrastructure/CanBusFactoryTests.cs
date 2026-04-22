using CanMonitor.Infrastructure.Can.Virtual;
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
        factory.Known.Should().ContainSingle()
            .Which.Kind.Should().Be(AdapterKind.Virtual);
    }

    [Fact]
    public void Create_virtual_returns_VirtualCanBus()
    {
        var factory = new CanBusFactory();
        var bus = factory.Create(AdapterKind.Virtual);
        bus.Should().BeOfType<VirtualCanBus>();
    }

    [Fact]
    public void Create_unsupported_throws()
    {
        var factory = new CanBusFactory();
        var act = () => factory.Create((AdapterKind)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
