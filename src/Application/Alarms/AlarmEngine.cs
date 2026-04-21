using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public sealed class AlarmEngine : IAlarmEngine
{
    private readonly IReadOnlyList<IAlarmRule> _rules;
    private readonly Subject<AlarmState> _changes = new();
    private ImmutableDictionary<string, AlarmState> _states = ImmutableDictionary<string, AlarmState>.Empty;

    public AlarmEngine(IEnumerable<IAlarmRule> rules)
    {
        _rules = rules.ToArray();
    }

    public IObservable<AlarmState> AlarmChanges => _changes.AsObservable();

    public IReadOnlyCollection<AlarmState> CurrentAlarms =>
        Volatile.Read(ref _states).Values.ToArray();

    public void Submit(SignalValue value)
    {
        var current = Volatile.Read(ref _states);
        var next = current;
        var diffs = new List<AlarmState>();

        foreach (var rule in _rules)
        {
            current.TryGetValue(rule.Code, out var prior);
            var updated = rule.Evaluate(value, prior);
            if (updated is null) continue;
            next = next.SetItem(rule.Code, updated);
            diffs.Add(updated);
        }

        if (!ReferenceEquals(current, next))
            Interlocked.Exchange(ref _states, next);

        foreach (var state in diffs)
            _changes.OnNext(state);
    }
}
