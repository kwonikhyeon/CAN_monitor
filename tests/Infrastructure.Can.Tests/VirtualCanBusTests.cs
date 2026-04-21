using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Infrastructure.Can.Tests;

public sealed class VirtualCanBusTests
{
    [Fact]
    public async Task Injected_frame_is_observed_as_Rx()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var captured = new List<CanFrame>();
        using var _ = bus.Frames.Subscribe(captured.Add);

        bus.Inject(new CanFrame(0x100, false, new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        captured.Should().ContainSingle();
        captured[0].Id.Should().Be(0x100u);
        captured[0].Direction.Should().Be(CanDirection.Rx);
    }

    [Fact]
    public void Factory_lists_Virtual_adapter_and_creates_VirtualCanBus()
    {
        var factory = new CanBusFactory();
        factory.AvailableAdapters.Should().Contain("Virtual");

        var bus = factory.Create("Virtual");
        bus.Should().BeOfType<VirtualCanBus>();
        bus.Name.Should().Be("Virtual");
    }

    [Fact]
    public void Factory_throws_for_unknown_adapter()
    {
        var factory = new CanBusFactory();
        Action act = () => factory.Create("NonExistent");
        act.Should().Throw<ArgumentException>().WithMessage("*NonExistent*");
    }
}
