using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class AlarmEngineTests
{
    private sealed class ThresholdRule : IAlarmRule
    {
        private readonly string _message;
        private readonly string _signal;
        private readonly double _threshold;
        public ThresholdRule(string code, string message, string signal, double threshold)
        { Code = code; _message = message; _signal = signal; _threshold = threshold; }

        public string Code { get; }
        public AlarmState? Evaluate(SignalValue value, AlarmState? current)
        {
            if (value.MessageName != _message || value.SignalName != _signal) return null;
            var shouldBeActive = value.PhysicalValue > _threshold;
            var currentActive = current?.Active ?? false;
            if (shouldBeActive == currentActive) return null;
            return new AlarmState(Code, AlarmSeverity.Warning,
                shouldBeActive ? "high" : "ok", shouldBeActive, value.Timestamp);
        }
    }

    private static SignalValue SV(string msg, string sig, double v) =>
        new(msg, sig, v, v, null, DateTimeOffset.UtcNow);

    [Fact]
    public void Emits_when_rule_first_activates()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });

        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("M", "S", 5));
        engine.Submit(SV("M", "S", 15));

        emitted.Should().ContainSingle().Which.Active.Should().BeTrue();
        engine.CurrentAlarms.Should().ContainSingle(a => a.Code == "EL.TEST" && a.Active);
    }

    [Fact]
    public void Emits_transition_back_to_inactive()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });
        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("M", "S", 15));
        engine.Submit(SV("M", "S", 3));

        emitted.Should().HaveCount(2);
        emitted[0].Active.Should().BeTrue();
        emitted[1].Active.Should().BeFalse();
        engine.CurrentAlarms.Single(a => a.Code == "EL.TEST").Active.Should().BeFalse();
    }

    [Fact]
    public void Does_not_emit_for_unchanged_state()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });
        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("M", "S", 15));
        engine.Submit(SV("M", "S", 20));
        engine.Submit(SV("M", "S", 25));

        emitted.Should().ContainSingle();
    }

    [Fact]
    public void Ignores_signals_not_matched_by_any_rule()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });
        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("OTHER", "X", 999));

        emitted.Should().BeEmpty();
        engine.CurrentAlarms.Should().BeEmpty();
    }
}
