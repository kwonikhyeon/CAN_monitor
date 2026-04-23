using CanMonitor.Application.Alarms;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class AlarmEngineReplaceRulesTests
{
    private static SignalValue Bit(string sig, double raw)
        => new("Alarms_0x200", sig, raw, raw, null, DateTimeOffset.UtcNow);

    private static IAlarmRule Rule(string sig)
        => new BitAlarmRule(sig, AlarmSeverity.Warning, "Alarms_0x200", sig, sig);

    [Fact]
    public void ReplaceRules_dismisses_active_alarms_and_resets_state()
    {
        using var sut = new AlarmEngine(new[] { Rule("EEC1_Timeout"), Rule("Pedal_Low") });
        var seen = new List<AlarmState>();
        using var sub = sut.AlarmChanges.Subscribe(seen.Add);

        sut.Submit(Bit("EEC1_Timeout", 1));
        sut.Submit(Bit("Pedal_Low", 1));
        sut.CurrentAlarms.Where(a => a.Active).Should().HaveCount(2);

        sut.ReplaceRules(Array.Empty<IAlarmRule>());

        sut.CurrentAlarms.Should().BeEmpty();
        seen.Where(a => !a.Active).Select(a => a.Code)
            .Should().BeEquivalentTo(new[] { "EEC1_Timeout", "Pedal_Low" });
    }

    [Fact]
    public void ReplaceRules_lets_new_rules_emit_active_on_next_Submit()
    {
        using var sut = new AlarmEngine(Array.Empty<IAlarmRule>());
        sut.ReplaceRules(new[] { Rule("EEC1_Timeout") });

        var seen = new List<AlarmState>();
        using var sub = sut.AlarmChanges.Subscribe(seen.Add);

        sut.Submit(Bit("EEC1_Timeout", 1));

        seen.Should().ContainSingle().Which.Active.Should().BeTrue();
    }

    [Fact]
    public void ReplaceRules_with_no_prior_active_emits_nothing()
    {
        using var sut = new AlarmEngine(new[] { Rule("EEC1_Timeout") });
        var seen = new List<AlarmState>();
        using var sub = sut.AlarmChanges.Subscribe(seen.Add);

        sut.ReplaceRules(new[] { Rule("Pedal_Low") });

        seen.Should().BeEmpty();
    }
}
