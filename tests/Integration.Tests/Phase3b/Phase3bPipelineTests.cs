using System.Collections.Immutable;
using System.Reactive.Linq;
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests.Phase3b;

public sealed class Phase3bPipelineTests
{
    // InMemoryDbcProvider: DatabaseReplaced 이벤트를 발신하는 가짜 DBC 제공자
    private sealed class InMemoryDbcProvider : IDbcProvider
    {
        public InMemoryDbcProvider(DbcDatabase initial) { Current = initial; }
        public DbcDatabase Current { get; private set; }
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public Task LoadAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;

        public void Replace(DbcDatabase next)
        {
            Current = next;
            DatabaseReplaced?.Invoke(this, next);
        }
    }

    // EEC1_Timeout 비트(startBit=25, length=1)를 포함한 Alarms_0x200 payload 생성
    // Motorola(big-endian) startBit=25 → byte[3], bit 1
    private static byte[] MakeAlarmsPayload(bool eec1Timeout)
    {
        var data = new byte[8];
        if (eec1Timeout) data[3] = 1 << 1;  // byte[3] 의 bit 1 설정
        return data;
    }

    // Alarms_0x200 DBC 메시지 구성 (EEC1_Timeout 신호 1개)
    private static DbcDatabase MakeAlarmsDb()
    {
        var msg = new DbcMessage(
            Id: 0x200,
            IsExtended: false,
            Name: "Alarms_0x200",
            Dlc: 8,
            Signals: ImmutableArray.Create(
                new DbcSignal("EEC1_Timeout", 25, 1, false, false, 1, 0, 0, 1, null, null)),
            CycleTime: null);
        return new DbcDatabase(new[] { msg });
    }

    // 첫 번째 통합 테스트: 비트 0→1→0 상태 변화에 따른 Active/Inactive 알람 발신
    [Fact]
    public async Task Pipeline_emits_alarm_active_then_inactive_for_eec1_timeout_bit()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var dbc = new InMemoryDbcProvider(MakeAlarmsDb());
        using var decoder = new SignalDecoder(dbc);
        using var alarms = new AlarmEngine(AlarmRuleFactory.FromDbc(dbc.Current));
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarms);

        var seen = new List<AlarmState>();
        using var alarmSub = alarms.AlarmChanges.Subscribe(seen.Add);
        using var pipeSub = pipeline.DecodedSignals.Subscribe();

        // EEC1_Timeout 비트 1 → Active 알람 발신
        bus.Inject(new CanFrame(0x200, false, MakeAlarmsPayload(eec1Timeout: true), DateTimeOffset.UtcNow, CanDirection.Rx));
        await Task.Delay(100);

        // EEC1_Timeout 비트 0 → Inactive 알람 발신
        bus.Inject(new CanFrame(0x200, false, MakeAlarmsPayload(eec1Timeout: false), DateTimeOffset.UtcNow, CanDirection.Rx));
        await Task.Delay(100);

        seen.Should().HaveCount(2);
        seen[0].Code.Should().Be("EEC1_Timeout");
        seen[0].Active.Should().BeTrue();
        seen[1].Code.Should().Be("EEC1_Timeout");
        seen[1].Active.Should().BeFalse();
    }

    // 두 번째 통합 테스트: DBC 교체 시 Active 알람이 Inactive로 dismiss 됨
    [Fact]
    public async Task ReplaceRules_dismisses_active_alarm_when_dbc_changes()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var dbc = new InMemoryDbcProvider(MakeAlarmsDb());
        using var decoder = new SignalDecoder(dbc);
        using var alarms = new AlarmEngine(AlarmRuleFactory.FromDbc(dbc.Current));
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarms);

        var seen = new List<AlarmState>();
        using var alarmSub = alarms.AlarmChanges.Subscribe(seen.Add);
        using var pipeSub = pipeline.DecodedSignals.Subscribe();

        // 비트 1로 Active 알람 발신
        bus.Inject(new CanFrame(0x200, false, MakeAlarmsPayload(eec1Timeout: true), DateTimeOffset.UtcNow, CanDirection.Rx));
        await Task.Delay(100);
        seen.Should().HaveCount(1);

        // DBC 교체: 새 DB는 빈 메시지셋 → FromDbc는 빈 룰 반환 → ReplaceRules이 dismiss
        dbc.Replace(DbcDatabase.Empty);
        alarms.ReplaceRules(AlarmRuleFactory.FromDbc(dbc.Current));

        seen.Should().HaveCount(2);
        seen[1].Code.Should().Be("EEC1_Timeout");
        seen[1].Active.Should().BeFalse();
    }
}
