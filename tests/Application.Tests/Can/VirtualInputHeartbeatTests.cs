using CanMonitor.Application.Can;
using CanMonitor.Application.Services;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class VirtualInputHeartbeatTests
{
    [Fact]
    public void Defaults_disabled_50ms_extended_id()
    {
        using var svc = new VirtualInputService();
        using var sut = new VirtualInputHeartbeat(svc);
        sut.Name.Should().Be("VirtualInput");
        sut.Period.Should().Be(TimeSpan.FromMilliseconds(50));
        sut.Enabled.Should().BeFalse();

        var frame = sut.BuildFrame();
        frame.Id.Should().Be(0x18FF5080u);
        frame.IsExtended.Should().BeTrue();
        frame.Direction.Should().Be(CanDirection.Tx);
        frame.Data.Length.Should().Be(8);
    }

    [Fact]
    public void Encodes_state_into_bytes_0_and_1_and_2()
    {
        using var svc = new VirtualInputService();
        using var sut = new VirtualInputHeartbeat(svc);

        svc.Update(new VirtualInputState(
            GearLever: GearLever.Forward,       // 2
            RangeShift: RangeShift.Second,      // 2
            TemperatureSwitch: true,
            ClutchPedalPercent: 75,
            PtoSwitch: true,
            FourWdSwitch: false,
            InchingSwitch: true,
            ParkingSwitch: false));

        var data = sut.BuildFrame().Data.Span;

        // Byte 0 = 0b_0001_1010: TempSwitch(bit4)=1, RangeShift(bits3..2)=10, GearLever(bits1..0)=10
        data[0].Should().Be(0b0001_1010);

        // Byte 1 = 0b_0000_0101: Parking=0(bit3), Inching=1(bit2), FourWd=0(bit1), Pto=1(bit0)
        data[1].Should().Be(0b0000_0101);

        data[2].Should().Be(75);
    }

    [Fact]
    public void Encodes_speed_sensors_big_endian_into_bytes_4_to_7()
    {
        using var svc = new VirtualInputService();
        using var sut = new VirtualInputHeartbeat(svc);

        svc.Update(new VirtualInputState(
            SpeedSensor1Rpm: 2500,  // 0x09C4
            SpeedSensor2Rpm: 45000)); // 0xAFC8

        var data = sut.BuildFrame().Data.Span;
        data[4].Should().Be(0x09); data[5].Should().Be(0xC4);
        data[6].Should().Be(0xAF); data[7].Should().Be(0xC8);
    }

    [Fact]
    public void Clamps_out_of_range_values()
    {
        using var svc = new VirtualInputService();
        using var sut = new VirtualInputHeartbeat(svc);

        svc.Update(new VirtualInputState(
            ClutchPedalPercent: 500,   // >100 → clamp 100
            SpeedSensor1Rpm: 99999));  // > 65535 → clamp 65535

        var data = sut.BuildFrame().Data.Span;
        data[2].Should().Be(100);
        data[4].Should().Be(0xFF); data[5].Should().Be(0xFF);
    }
}
