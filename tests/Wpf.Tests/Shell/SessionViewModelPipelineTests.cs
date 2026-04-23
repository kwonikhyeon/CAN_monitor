using System.Collections.Immutable;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class SessionViewModelPipelineTests
{
    private sealed class FakeDbcProvider : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = DbcDatabase.Empty;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;

        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            // Speed 신호를 포함한 Status_0x100 메시지 생성
            var msg = new DbcMessage(0x100, false, "Status_0x100", 8,
                ImmutableArray.Create(new DbcSignal("Speed", 0, 16, true, false, 1, 0, 0, 65535, "rpm", null)),
                null);
            Current = new DbcDatabase(new[] { msg });
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }

        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubBus : ICanBus
    {
        public string Name => "Stub";
        public bool IsOpen { get; private set; }
        public Subject<CanFrame> RxSubject { get; } = new();
        public IObservable<CanFrame> Frames => RxSubject;

        public Task OpenAsync(CanBusOptions options, CancellationToken ct = default)
        {
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            IsOpen = false;
            return Task.CompletedTask;
        }

        public Task SendAsync(CanFrame frame, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            RxSubject.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubFactory : Wpf.Infrastructure.ICanBusFactory
    {
        public StubBus Bus { get; } = new();
        public IReadOnlyList<AdapterOption> Known { get; } =
            new[] { new AdapterOption(AdapterKind.Virtual, "Stub") };

        public ICanBus Create(AdapterKind kind) => Bus;
    }

    private static string CreateTempDbc(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "VERSION \"\"\n");
        return path;
    }

    [Fact]
    public async Task ConnectAsync_assembles_pipeline_and_decoded_signals_flow_through_hub()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-pipe-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var factory = new StubFactory();
        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var publisher = new ManualBusStatusPublisher();
        var alarms = new AlarmEngine(Array.Empty<IAlarmRule>());
        var decoder = new CanMonitor.Dbc.SignalDecoder(dbc);
        var vm = new SessionViewModel(factory, dbc, hub, publisher, alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();
        await vm.ConnectCommand.ExecuteAsync(null);

        var seen = new List<SignalValue>();
        using var sub = hub.Signals.Subscribe(seen.Add);

        // 0x100 (Status_0x100) 프레임 전송: Speed 신호 = 0x09C4 (2500 RPM)
        factory.Bus.RxSubject.OnNext(new CanFrame(0x100, false,
            new byte[] { 0x09, 0xC4, 0, 0, 0, 0, 0, 0 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        await Task.Delay(100);  // pool scheduler hop 대기

        // 디코딩된 신호가 hub을 통해 흘러갔는지 확인
        seen.Should().NotBeEmpty();
        seen.Should().Contain(s => s.SignalName == "Speed");
    }

    [Fact]
    public async Task DisconnectAsync_clears_pipeline_reference()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-pipe-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var factory = new StubFactory();
        var dbc = new FakeDbcProvider();
        var alarms = new AlarmEngine(Array.Empty<IAlarmRule>());
        var decoder = new CanMonitor.Dbc.SignalDecoder(dbc);
        var vm = new SessionViewModel(factory, dbc, new CanEventHub(),
            new ManualBusStatusPublisher(), alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();
        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.DisconnectAsync();

        // 두 번째 Connect가 새 파이프라인을 만들어 throw 없이 동작
        await vm.ConnectCommand.ExecuteAsync(null);
        vm.State.Should().Be(ConnectionState.Connected);
    }
}
