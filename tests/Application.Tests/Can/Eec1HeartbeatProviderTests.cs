using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class Eec1HeartbeatProviderTests
{
    [Fact]
    public void Defaults_are_enabled_100ms_extended_id()
    {
        using var sut = new Eec1HeartbeatProvider();
        sut.Name.Should().Be("EEC1");
        sut.Period.Should().Be(TimeSpan.FromMilliseconds(100));
        sut.Enabled.Should().BeTrue();

        var frame = sut.BuildFrame();
        frame.Id.Should().Be(0x18F00417u);
        frame.IsExtended.Should().BeTrue();
        frame.Direction.Should().Be(CanDirection.Tx);
        frame.Data.Length.Should().Be(8);
        frame.Data.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void SetLow_and_SetHigh_pack_into_byte0_nibbles()
    {
        using var sut = new Eec1HeartbeatProvider();
        sut.SetLow(0x07);
        sut.SetHigh(0x0A);
        var frame = sut.BuildFrame();
        frame.Data.Span[0].Should().Be(0xA7);
        for (int i = 1; i < 8; i++)
            frame.Data.Span[i].Should().Be(0x00);
    }

    [Fact]
    public void SetLow_clamps_to_4bit_range()
    {
        using var sut = new Eec1HeartbeatProvider();
        sut.SetLow(255);
        sut.BuildFrame().Data.Span[0].Should().Be(0x0F);
        sut.SetLow(-1);
        sut.BuildFrame().Data.Span[0].Should().Be(0x00);
    }

    [Fact]
    public async Task SetEnabled_emits_single_change()
    {
        using var sut = new Eec1HeartbeatProvider();
        var changes = new List<bool>();
        using var _ = sut.EnabledChanges.Subscribe(changes.Add);

        sut.SetEnabled(true);   // no-op, already true
        sut.SetEnabled(false);
        sut.SetEnabled(false);  // no-op
        sut.SetEnabled(true);

        await Task.Delay(10);
        changes.Should().Equal(true, false, true);  // BehaviorSubject replays current
    }
}
