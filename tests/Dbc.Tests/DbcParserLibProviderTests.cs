using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Dbc.Tests;

public sealed class DbcParserLibProviderTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Current_is_Empty_before_load()
    {
        var sut = new DbcParserLibProvider();
        sut.Current.Should().BeSameAs(DbcDatabase.Empty);
    }

    [Fact]
    public async Task LoadAsync_replaces_snapshot()
    {
        var sut = new DbcParserLibProvider();
        await sut.LoadAsync(Fixture("simple.dbc"));

        sut.Current.Messages.Should().HaveCount(1);
        var msg = sut.Current.MessagesById[256u];
        msg.Name.Should().Be("TestMsg");
        msg.Dlc.Should().Be(8);
        msg.CycleTime.Should().Be(TimeSpan.FromMilliseconds(100));
        msg.Signals.Should().HaveCount(2);

        var flag = msg.Signals.Single(s => s.Name == "Flag");
        flag.StartBit.Should().Be(0);
        flag.Length.Should().Be(1);
        flag.LittleEndian.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_raises_DatabaseReplaced()
    {
        var sut = new DbcParserLibProvider();
        DbcDatabase? observed = null;
        sut.DatabaseReplaced += (_, db) => observed = db;

        await sut.LoadAsync(Fixture("simple.dbc"));

        observed.Should().NotBeNull();
        observed.Should().BeSameAs(sut.Current);
    }
}
