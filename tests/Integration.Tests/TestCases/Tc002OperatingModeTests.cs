using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Linq;
using Xunit;

namespace CanMonitor.Integration.Tests.TestCases;

public sealed class Tc002OperatingModeTests
{
    private sealed class StubDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame)
        {
            if (frame.Id == 0x0C000E00)
            {
                return new[]
                {
                    new SignalValue("Operating_Mode", "Driving_Status", 1, 1, null, frame.Timestamp),
                    new SignalValue("Operating_Mode", "Working_Status", 0, 0, null, frame.Timestamp),
                };
            }
            return Array.Empty<SignalValue>();
        }

        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    [Fact]
    public async Task TC_002_driving_bit_true_observed_via_pipeline()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var alarm = new AlarmEngine(Array.Empty<IAlarmRule>());
        var pipeline = new CanReceivePipeline(bus.Frames, new StubDecoder(), alarm);

        using var pipelineSub = pipeline.DecodedSignals.Subscribe(_ => { });
        var ctx = new TestRunnerContext(bus, pipeline.DecodedSignals, alarm.AlarmChanges, new StubDecoder());
        var runner = new TestRunner(new IStepExecutor[] { new ObserveBitStepExecutor() }, ctx);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bus.Inject(new CanFrame(0x0C000E00, true, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(50);
            }
        });

        var testCase = new TestCase(
            "TC-002", "OperatingMode", "정상모드 Driving",
            Prerequisites: Array.Empty<TestStep>(),
            Steps: new TestStep[]
            {
                new ObserveBitStep("Operating_Mode", "Driving_Status", true, TimeSpan.FromMilliseconds(500)),
                new ObserveBitStep("Operating_Mode", "Working_Status", false, TimeSpan.FromMilliseconds(500)),
            });

        var result = await runner.RunAsync(testCase);
        cts.Cancel();

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().HaveCount(2).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }
}
