using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Models;

public sealed class CanFrameTests
{
    [Fact]
    public void Constructs_with_all_fields()
    {
        var ts = new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var frame = new CanFrame(
            Id: 0x18F00417,
            IsExtended: true,
            Data: data,
            Timestamp: ts,
            Direction: CanDirection.Rx);

        frame.Id.Should().Be(0x18F00417u);
        frame.IsExtended.Should().BeTrue();
        frame.Data.ToArray().Should().Equal(0x01, 0x02, 0x03);
        frame.Timestamp.Should().Be(ts);
        frame.Direction.Should().Be(CanDirection.Rx);
    }

    [Fact]
    public void CanDirection_has_Rx_and_Tx_values()
    {
        Enum.GetValues<CanDirection>().Should().BeEquivalentTo(
            new[] { CanDirection.Rx, CanDirection.Tx });
    }

    [Fact]
    public void With_expression_creates_copy_with_different_direction()
    {
        var original = new CanFrame(0x100, false, new byte[] { 0xFF }, DateTimeOffset.UtcNow, CanDirection.Rx);
        var echo = original with { Direction = CanDirection.Tx };

        echo.Direction.Should().Be(CanDirection.Tx);
        echo.Id.Should().Be(original.Id);
        original.Direction.Should().Be(CanDirection.Rx);
    }
}
