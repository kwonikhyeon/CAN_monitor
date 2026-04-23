using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public static class AlarmRuleFactory
{
    private const string AlarmMessageName = "Alarms_0x200";

    public static IReadOnlyList<IAlarmRule> FromDbc(DbcDatabase db)
    {
        // Alarms_0x200 메시지 찾기
        var msg = db.Messages.FirstOrDefault(m => m.Name == AlarmMessageName);
        if (msg is null) return Array.Empty<IAlarmRule>();

        var rules = new List<IAlarmRule>(msg.Signals.Length);
        foreach (var sig in msg.Signals)
        {
            // 1비트 신호만 선택
            if (sig.Length != 1) continue;
            rules.Add(new BitAlarmRule(
                code: sig.Name,
                severity: AlarmSeverity.Warning,
                messageName: msg.Name,
                signalName: sig.Name,
                description: sig.Name));
        }
        return rules;
    }
}
