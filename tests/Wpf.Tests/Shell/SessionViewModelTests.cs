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

public class SessionViewModelTests
{
    private sealed class FakeDbcProvider : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = DbcDatabase.Empty;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public bool ShouldFail { get; set; }
        public List<string> LoadedPaths { get; } = new();

        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            LoadedPaths.Add(path);
            if (ShouldFail) throw new InvalidOperationException("fake failure");
            // Alarms_0x200 메시지와 EEC1_Timeout 신호 생성
            var msg = new DbcMessage(0x200, false, "Alarms_0x200", 8,
                ImmutableArray.Create(
                    new DbcSignal("EEC1_Timeout", 25, 1, false, false, 1, 0, 0, 1, null, null)),
                null);
            Current = new DbcDatabase(new[] { msg });
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }

        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static string CreateTempDbc(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "VERSION \"\"\n");
        return path;
    }

    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) { }
    }

    private sealed class FakeDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    private static SessionViewModel CreateVm(string rootDir, FakeDbcProvider dbc, CanBusFactory factory,
        CanEventHub hub, ManualBusStatusPublisher publisher)
        => new SessionViewModel(factory, dbc, hub, publisher,
            new FakeAlarmEngine(), new FakeDecoder(),
            Array.Empty<IBusHeartbeatProvider>(), rootDir);

    [Fact]
    public async Task InitializeAsync_loads_default_dbc_when_present()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);

        await vm.InitializeAsync();

        vm.DbcFiles.Should().ContainSingle(f => f.DisplayName == "120HP_NoPto.dbc");
        vm.SelectedDbc.Should().NotBeNull();
        dbc.LoadedPaths.Should().ContainSingle();
        vm.State.Should().Be(ConnectionState.Disconnected);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task InitializeAsync_sets_error_on_load_failure()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider { ShouldFail = true };
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);

        await vm.InitializeAsync();

        vm.State.Should().Be(ConnectionState.Error);
        vm.ErrorMessage.Should().Contain("fake failure");
    }

    [Fact]
    public async Task ConnectCommand_publishes_Connected_status()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);
        await vm.InitializeAsync();

        BusStatusChange? lastStatus = null;
        using var sub = pub.Changes.Subscribe(c => lastStatus = c);

        await vm.ConnectCommand.ExecuteAsync(null);

        vm.State.Should().Be(ConnectionState.Connected);
        lastStatus!.Status.Should().Be(BusStatus.Connected);
    }

    [Fact]
    public async Task DisconnectAsync_publishes_Disconnected_and_is_idempotent()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);
        await vm.InitializeAsync();
        await vm.ConnectCommand.ExecuteAsync(null);

        await vm.DisconnectAsync();
        vm.State.Should().Be(ConnectionState.Disconnected);

        await vm.DisconnectAsync();  // 두 번째 호출은 no-op 이어야 함
        vm.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task StateChanges_emits_on_transitions()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);
        await vm.InitializeAsync();

        var emitted = new List<ConnectionState>();
        using var sub = vm.StateChanges.Subscribe(s => emitted.Add(s));

        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.DisconnectAsync();

        emitted.Should().ContainInOrder(
            ConnectionState.Disconnected,  // BehaviorSubject 재방출
            ConnectionState.Connecting,
            ConnectionState.Connected,
            ConnectionState.Disconnected);
    }
}
