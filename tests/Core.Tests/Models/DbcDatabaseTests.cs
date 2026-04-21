using System.Collections.Immutable;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Models;

public sealed class DbcDatabaseTests
{
    [Fact]
    public void Empty_database_has_no_messages()
    {
        var db = DbcDatabase.Empty;
        db.Messages.Should().BeEmpty();
        db.MessagesById.Should().BeEmpty();
    }

    [Fact]
    public void Constructs_lookup_indexes_from_messages()
    {
        var sig = new DbcSignal("Gear_N", 7, 1, LittleEndian: false, IsSigned: false,
            Factor: 1.0, Offset: 0.0, Minimum: 0, Maximum: 1, Unit: null, ValueTable: null);
        var msg = new DbcMessage(
            Id: 0x0C000E00, IsExtended: true, Name: "Status_0x0C000E00", Dlc: 8,
            Signals: ImmutableArray.Create(sig), CycleTime: TimeSpan.FromMilliseconds(100));

        var db = new DbcDatabase(new[] { msg });

        db.Messages.Should().ContainSingle().Which.Should().Be(msg);
        db.MessagesById[0x0C000E00u].Should().Be(msg);
    }

    [Fact]
    public void Duplicate_message_id_throws()
    {
        var sig = new DbcSignal("x", 0, 1, false, false, 1, 0, 0, 1, null, null);
        var m1 = new DbcMessage(0x100, false, "A", 8, ImmutableArray.Create(sig), null);
        var m2 = new DbcMessage(0x100, false, "B", 8, ImmutableArray.Create(sig), null);

        Action act = () => new DbcDatabase(new[] { m1, m2 });
        act.Should().Throw<ArgumentException>().WithMessage("*duplicate*0x100*");
    }
}
