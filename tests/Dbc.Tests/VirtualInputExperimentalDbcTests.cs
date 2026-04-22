using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Dbc.Tests;

public sealed class VirtualInputExperimentalDbcTests
{
    private const uint VirtualInputFrameId = 0x18FF5080u;

    private static string Experimental(string name) =>
        Path.Combine(AppContext.BaseDirectory, "experimental", name);

    [Fact]
    public async Task Motorola_variant_exposes_big_endian_MSB_start_bits()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Experimental("virtual_input.motorola.dbc"));

        var msg = sut.Current.MessagesById[VirtualInputFrameId];
        msg.Name.Should().Be("VirtualInput");
        msg.Dlc.Should().Be(8);
        msg.Signals.Should().HaveCount(11);

        Tuple(msg, "GearLever").Should().Be((1, 2, false));
        Tuple(msg, "RangeShift").Should().Be((3, 2, false));
        Tuple(msg, "TempSwitch").Should().Be((4, 1, false));
        Tuple(msg, "PtoSwitch").Should().Be((8, 1, false));
        Tuple(msg, "FourWdSwitch").Should().Be((9, 1, false));
        Tuple(msg, "InchingSwitch").Should().Be((10, 1, false));
        Tuple(msg, "ParkingSwitch").Should().Be((11, 1, false));
        Tuple(msg, "PedalPercent").Should().Be((23, 8, false));
        Tuple(msg, "PedalVoltage").Should().Be((31, 8, false));
        Tuple(msg, "SpeedSensor1").Should().Be((39, 16, false));
        Tuple(msg, "SpeedSensor2").Should().Be((55, 16, false));
    }

    [Fact]
    public async Task Intel_variant_exposes_little_endian_LSB_start_bits()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Experimental("virtual_input.intel.dbc"));

        var msg = sut.Current.MessagesById[VirtualInputFrameId];
        msg.Name.Should().Be("VirtualInput");
        msg.Dlc.Should().Be(8);
        msg.Signals.Should().HaveCount(11);

        Tuple(msg, "GearLever").Should().Be((0, 2, true));
        Tuple(msg, "RangeShift").Should().Be((2, 2, true));
        Tuple(msg, "TempSwitch").Should().Be((4, 1, true));
        Tuple(msg, "PtoSwitch").Should().Be((8, 1, true));
        Tuple(msg, "FourWdSwitch").Should().Be((9, 1, true));
        Tuple(msg, "InchingSwitch").Should().Be((10, 1, true));
        Tuple(msg, "ParkingSwitch").Should().Be((11, 1, true));
        Tuple(msg, "PedalPercent").Should().Be((16, 8, true));
        Tuple(msg, "PedalVoltage").Should().Be((24, 8, true));
        Tuple(msg, "SpeedSensor1").Should().Be((32, 16, true));
        Tuple(msg, "SpeedSensor2").Should().Be((48, 16, true));
    }

    private static (int StartBit, int Length, bool LittleEndian) Tuple(DbcMessage msg, string name)
    {
        var s = msg.Signals.Single(x => x.Name == name);
        return (s.StartBit, s.Length, s.LittleEndian);
    }
}
