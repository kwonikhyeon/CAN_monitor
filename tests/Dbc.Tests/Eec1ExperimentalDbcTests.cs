using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Dbc.Tests;

public sealed class Eec1ExperimentalDbcTests
{
    private const uint Eec1FrameId = 0x18F00417u;

    private static string Experimental(string name) =>
        Path.Combine(AppContext.BaseDirectory, "experimental", name);

    [Fact]
    public async Task Motorola_variant_exposes_big_endian_MSB_start_bits()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Experimental("eec1_emulation.motorola.dbc"));

        var msg = sut.Current.MessagesById[Eec1FrameId];
        msg.Name.Should().Be("EEC1");
        msg.Dlc.Should().Be(8);

        var low = msg.Signals.Single(s => s.Name == "EEC1_Low");
        (low.StartBit, low.Length, low.LittleEndian).Should().Be((3, 4, false));

        var high = msg.Signals.Single(s => s.Name == "EEC1_High");
        (high.StartBit, high.Length, high.LittleEndian).Should().Be((7, 4, false));
    }

    [Fact]
    public async Task Intel_variant_exposes_little_endian_LSB_start_bits()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Experimental("eec1_emulation.intel.dbc"));

        var msg = sut.Current.MessagesById[Eec1FrameId];
        msg.Name.Should().Be("EEC1");
        msg.Dlc.Should().Be(8);

        var low = msg.Signals.Single(s => s.Name == "EEC1_Low");
        (low.StartBit, low.Length, low.LittleEndian).Should().Be((0, 4, true));

        var high = msg.Signals.Single(s => s.Name == "EEC1_High");
        (high.StartBit, high.Length, high.LittleEndian).Should().Be((4, 4, true));
    }
}
