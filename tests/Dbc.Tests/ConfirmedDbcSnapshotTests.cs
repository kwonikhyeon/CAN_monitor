using CanMonitor.Dbc;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Dbc.Tests;

public sealed class ConfirmedDbcSnapshotTests
{
    private static string Confirmed(string name) =>
        Path.Combine(AppContext.BaseDirectory, "confirmed", name);

    [Fact]
    public async Task Loads_120HP_NoPto_dbc_with_expected_shape()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Confirmed("120HP_NoPto.dbc"));

        sut.Current.Messages.Should().HaveCount(2);
        sut.Current.MessagesById.Should().ContainKeys(0x0C000E00u, 0x200u);

        var status = sut.Current.MessagesById[0x0C000E00u];
        status.CycleTime.Should().Be(TimeSpan.FromMilliseconds(100));
        status.Signals.Should().Contain(s => s.Name == "Gear_Lever_N_Status");

        var alarms = sut.Current.MessagesById[0x200u];
        alarms.CycleTime.Should().Be(TimeSpan.FromMilliseconds(1000));
        alarms.Signals.Should().Contain(s => s.Name == "EEC1_Timeout");
    }

    [Fact]
    public async Task Loads_160HP_WithPto_base_dbc_with_expected_shape()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Confirmed("160HP_WithPto.base.dbc"));

        sut.Current.Messages.Should().HaveCount(3);
        sut.Current.MessagesById.Should().ContainKeys(0x0C000E00u, 0x200u, 0x202u);

        sut.Current.MessagesById[0x202u].Signals
            .Should().Contain(s => s.Name == "PTO_Switch");
    }
}
