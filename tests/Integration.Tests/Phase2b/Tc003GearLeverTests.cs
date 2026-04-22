using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Application.Services;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests.Phase2b;

public sealed class Tc003GearLeverTests
{
    [Fact]
    public async Task TC003_VirtualInput_heartbeat_emits_GearLever_equals_Neutral()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        using var viService = new VirtualInputService();
        using var viHeartbeat = new VirtualInputHeartbeat(viService);
        using var eec1 = new Eec1HeartbeatProvider();

        await using var scheduler = new TxScheduler(bus);
        await using var lifecycle = new BusLifecycleService(
            new IBusHeartbeatProvider[] { eec1, viHeartbeat },
            scheduler);

        var ctx = new TestRunnerContext(
            bus,
            Observable.Empty<SignalValue>(),
            Observable.Empty<AlarmState>(),
            null!);

        var runner = new TestRunner(
            new IStepExecutor[]
            {
                new SetHeartbeatStepExecutor(new IBusHeartbeatProvider[] { eec1, viHeartbeat }),
                new EnterSimulationModeStepExecutor(viService, new IBusHeartbeatProvider[] { eec1, viHeartbeat }),
                new SetVirtualInputStepExecutor(viService),
                new WaitStepExecutor(),
            },
            ctx);

        var captured = new List<CanFrame>();
        using var sub = bus.Frames
            .Where(f => f.Id == 0x18FF5080u && f.IsExtended)
            .Subscribe(captured.Add);

        lifecycle.Start();

        var testCase = new TestCase(
            "TC-003",
            "GearLeverN",
            "Gear Lever Neutral emits VirtualInput frame with GearLever=Neutral",
            Prerequisites: Array.Empty<TestStep>(),
            Steps: new TestStep[]
            {
                new SetHeartbeatStep("EEC1", false),
                new EnterSimulationModeStep(),
                new SetVirtualInputStep(GearLever: "NEUTRAL"),
                new WaitStep(TimeSpan.FromMilliseconds(250)),
            });

        var result = await runner.RunAsync(testCase);

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().HaveCount(4).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);

        captured.Should().NotBeEmpty("VirtualInput heartbeat should emit after SetEnabled(true)");
        var last = captured[^1];
        (last.Data.Span[0] & 0x03).Should().Be((int)GearLever.Neutral);
    }
}
