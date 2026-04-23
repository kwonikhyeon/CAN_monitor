using System.Collections.Immutable;
using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class AlarmRuleFactoryTests
{
    private static DbcSignal Bit(string name, int startBit) => new(
        Name: name,
        StartBit: startBit,
        Length: 1,
        LittleEndian: false,
        IsSigned: false,
        Factor: 1.0,
        Offset: 0.0,
        Minimum: 0.0,
        Maximum: 1.0,
        Unit: null,
        ValueTable: null);

    [Fact]
    public void FromDbc_creates_rule_per_alarm_bit_signal()
    {
        var alarms = new DbcMessage(
            Id: 0x200,
            IsExtended: false,
            Name: "Alarms_0x200",
            Dlc: 8,
            Signals: ImmutableArray.Create(
                Bit("EEC1_Timeout", 25),
                Bit("Pressure_1_Fault", 16),
                Bit("Pedal_Failure", 2)),
            CycleTime: null);

        var db = new DbcDatabase(new[] { alarms });

        var rules = AlarmRuleFactory.FromDbc(db);

        rules.Select(r => r.Code).Should().BeEquivalentTo(
            new[] { "EEC1_Timeout", "Pressure_1_Fault", "Pedal_Failure" });
    }

    [Fact]
    public void FromDbc_skips_non_bit_signals()
    {
        var alarms = new DbcMessage(
            Id: 0x200,
            IsExtended: false,
            Name: "Alarms_0x200",
            Dlc: 8,
            Signals: ImmutableArray.Create(
                Bit("EEC1_Timeout", 25),
                new DbcSignal("MultiBitField", 0, 4, false, false, 1, 0, 0, 15, null, null)),
            CycleTime: null);

        var db = new DbcDatabase(new[] { alarms });

        var rules = AlarmRuleFactory.FromDbc(db);

        rules.Select(r => r.Code).Should().BeEquivalentTo(new[] { "EEC1_Timeout" });
    }

    [Fact]
    public void FromDbc_returns_empty_when_no_alarm_message()
    {
        var other = new DbcMessage(
            Id: 0x100,
            IsExtended: false,
            Name: "OtherMsg",
            Dlc: 8,
            Signals: ImmutableArray.Create(Bit("Foo", 0)),
            CycleTime: null);
        var db = new DbcDatabase(new[] { other });

        var rules = AlarmRuleFactory.FromDbc(db);

        rules.Should().BeEmpty();
    }

    [Fact]
    public void FromDbc_returns_empty_for_empty_database()
    {
        AlarmRuleFactory.FromDbc(DbcDatabase.Empty).Should().BeEmpty();
    }
}
