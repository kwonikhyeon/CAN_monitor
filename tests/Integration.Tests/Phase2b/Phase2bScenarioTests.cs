using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Dbc;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests.Phase2b;

public sealed class Phase2bScenarioTests
{
    [Fact]
    public async Task TC024_EEC1_timeout_is_observed_after_heartbeat_disabled_and_tcu_reports()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var dbcProvider = new DbcParserLibProvider();
        await dbcProvider.LoadAsync("confirmed/120HP_NoPto.dbc");
        var decoder = new SignalDecoder(dbcProvider);

        var alarm = new AlarmEngine(Array.Empty<IAlarmRule>());
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarm);

        // Keep the pipeline hot by subscribing to it
        using var pipelineSub = pipeline.DecodedSignals.Subscribe(_ => { });

        var eec1 = new Eec1HeartbeatProvider();
        var ctx = new TestRunnerContext(bus, pipeline.DecodedSignals, alarm.AlarmChanges, decoder);
        var runner = new TestRunner(
            new IStepExecutor[]
            {
                new SetHeartbeatStepExecutor(new IBusHeartbeatProvider[] { eec1 }),
                new ObserveBitStepExecutor()
            },
            ctx);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                // Alarms_0x200 frame with EEC1_Timeout (bit 25) set
                // Motorola format: bit 25 = byte 3, bit 1 (0x02)
                var data = new byte[8];
                data[3] = 0x02; // Bit 25 set
                bus.Inject(new CanFrame(0x200, IsExtended: false, data, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(50);
            }
        });

        var testCase = new TestCase(
            "TC-024", "EEC1Timeout", "EEC1 timeout is observed after heartbeat disabled and TCU reports",
            Prerequisites: Array.Empty<TestStep>(),
            Steps: new TestStep[]
            {
                new SetHeartbeatStep("EEC1", false),
                new ObserveBitStep("Alarms_0x200", "EEC1_Timeout", true, TimeSpan.FromMilliseconds(500)),
            });

        var result = await runner.RunAsync(testCase);
        cts.Cancel();

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().HaveCount(2).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }
}
