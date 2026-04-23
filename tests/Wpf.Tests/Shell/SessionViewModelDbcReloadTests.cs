using System.Collections.Immutable;
using System.IO;
using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class SessionViewModelDbcReloadTests
{
    private sealed class FakeDbcProvider : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = DbcDatabase.Empty;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public List<string> LoadedPaths { get; } = new();

        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            LoadedPaths.Add(path);
            // Alarms_0x200 메시지와 EEC1_Timeout 신호 생성
            var msg = new DbcMessage(0x200, false, "Alarms_0x200", 8,
                ImmutableArray.Create(new DbcSignal("EEC1_Timeout", 25, 1, false, false, 1, 0, 0, 1, null, null)),
                null);
            Current = new DbcDatabase(new[] { msg });
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }

        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CapturingAlarmEngine : IAlarmEngine
    {
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public List<int> RuleCountAtReplace { get; } = new();

        public void Submit(SignalValue value) { }

        // ReplaceRules 호출 시마다 규칙 개수 기록
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) => RuleCountAtReplace.Add(rules.Count);
    }

    private sealed class FakeDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    private static string CreateTempDbc(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "VERSION \"\"\n");
        return path;
    }

    [Fact]
    public async Task InitializeAsync_loads_dbc_and_replaces_rules()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-rel-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var alarms = new CapturingAlarmEngine();
        var decoder = new FakeDecoder();
        var vm = new SessionViewModel(new CanBusFactory(), dbc, new CanEventHub(),
            new ManualBusStatusPublisher(), alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();

        // 정확히 한 번의 LoadAsync 호출 확인
        dbc.LoadedPaths.Should().ContainSingle();
        // 정확히 한 번의 ReplaceRules 호출 및 1개 규칙(EEC1_Timeout) 확인
        alarms.RuleCountAtReplace.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task SelectedDbc_change_triggers_reload_and_replace()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-rel-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");
        CreateTempDbc(confirmed, "160HP_WithPto.dbc");

        var dbc = new FakeDbcProvider();
        var alarms = new CapturingAlarmEngine();
        var decoder = new FakeDecoder();
        var vm = new SessionViewModel(new CanBusFactory(), dbc, new CanEventHub(),
            new ManualBusStatusPublisher(), alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();
        var initialLoads = dbc.LoadedPaths.Count;

        // 다른 DBC 파일 선택 (setter를 통한 fire-and-forget 트리거)
        var other = vm.DbcFiles.First(f => f.DisplayName == "160HP_WithPto.dbc");
        vm.SelectedDbc = other;

        await Task.Delay(50);  // fire-and-forget 완료 대기

        // 초기 로드 후 추가 로드 확인
        dbc.LoadedPaths.Count.Should().BeGreaterThan(initialLoads);
        // 초기 + 설정 후 최소 2번 ReplaceRules 호출
        alarms.RuleCountAtReplace.Should().HaveCountGreaterOrEqualTo(2);
    }
}
