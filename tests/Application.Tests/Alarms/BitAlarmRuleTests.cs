using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class BitAlarmRuleTests
{
    private static SignalValue Bit(string msg, string sig, double raw)
        => new(msg, sig, raw, raw, null, DateTimeOffset.UtcNow);

    [Fact]
    public void Activates_on_zero_to_one_transition()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");

        var result = rule.Evaluate(Bit("Alarms_0x200", "EEC1_Timeout", 1), prior: null);

        result.Should().NotBeNull();
        result!.Code.Should().Be("EEC1_Timeout");
        result.Severity.Should().Be(AlarmSeverity.Warning);
        result.Message.Should().Be("EEC1_Timeout");
        result.Active.Should().BeTrue();
    }

    [Fact]
    public void Deactivates_on_one_to_zero_transition()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");
        var prior = new AlarmState("EEC1_Timeout", AlarmSeverity.Warning, "EEC1_Timeout", true, DateTimeOffset.UtcNow);

        var result = rule.Evaluate(Bit("Alarms_0x200", "EEC1_Timeout", 0), prior: prior);

        result.Should().NotBeNull();
        result!.Active.Should().BeFalse();
    }

    [Fact]
    public void No_op_when_state_unchanged()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");
        var prior = new AlarmState("EEC1_Timeout", AlarmSeverity.Warning, "EEC1_Timeout", true, DateTimeOffset.UtcNow);

        var result = rule.Evaluate(Bit("Alarms_0x200", "EEC1_Timeout", 1), prior: prior);

        result.Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_wrong_message()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");

        var result = rule.Evaluate(Bit("OtherMsg", "EEC1_Timeout", 1), prior: null);

        result.Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_wrong_signal()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");

        var result = rule.Evaluate(Bit("Alarms_0x200", "Other_Bit", 1), prior: null);

        result.Should().BeNull();
    }
}
