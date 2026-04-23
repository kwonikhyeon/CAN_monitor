using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public sealed class AlarmEngine : IAlarmEngine, IDisposable
{
    private readonly Subject<AlarmState> _changes = new();
    // Submit와 ReplaceRules을 직렬화하여 _states와 _rules의 일관성을 보장
    private readonly object _sync = new();
    // volatile은 컴파일러 재정렬 방지
    private volatile IReadOnlyList<IAlarmRule> _rules;
    private ImmutableDictionary<string, AlarmState> _states = ImmutableDictionary<string, AlarmState>.Empty;

    public AlarmEngine(IEnumerable<IAlarmRule> rules)
    {
        _rules = rules.ToArray();
    }

    public IObservable<AlarmState> AlarmChanges => _changes.AsObservable();

    public IReadOnlyCollection<AlarmState> CurrentAlarms
    {
        get
        {
            lock (_sync) return _states.Values.ToArray();
        }
    }

    public void Submit(SignalValue value)
    {
        var diffs = new List<AlarmState>();
        lock (_sync)
        {
            var current = _states;
            var next = current;
            // ReplaceRules이 동시에 _rules을 업데이트하지 않도록 보호
            foreach (var rule in _rules)
            {
                current.TryGetValue(rule.Code, out var prior);
                var updated = rule.Evaluate(value, prior);
                if (updated is null) continue;
                next = next.SetItem(rule.Code, updated);
                diffs.Add(updated);
            }
            _states = next;
        }
        foreach (var state in diffs)
            _changes.OnNext(state);
    }

    public void ReplaceRules(IReadOnlyList<IAlarmRule> rules)
    {
        var dismissed = new List<AlarmState>();
        lock (_sync)
        {
            // 활성 알람을 모두 dismiss (비활성화)
            foreach (var prior in _states.Values)
            {
                if (!prior.Active) continue;
                dismissed.Add(prior with { Active = false, Since = DateTimeOffset.UtcNow });
            }
            // 상태와 룰을 초기화
            _states = ImmutableDictionary<string, AlarmState>.Empty;
            _rules = rules;
        }
        // 새 규칙이 Submit 시에 활성화될 수 있도록 lock 외부에서 알림 발송
        foreach (var state in dismissed)
            _changes.OnNext(state);
    }

    public void Dispose()
    {
        _changes.OnCompleted();
        _changes.Dispose();
    }
}
