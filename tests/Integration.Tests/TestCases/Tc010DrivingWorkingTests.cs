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

public sealed class Tc010DrivingWorkingTests
{
    private sealed class ModeStubDecoder : ISignalDecoder
    {
        private volatile int _drivingFlag = 1;
        public void SetWorking() { _drivingFlag = 0; }

        public IReadOnlyList<SignalValue> Decode(CanFrame frame)
        {
            if (frame.Id != 0x0C000E00) return Array.Empty<SignalValue>();
            var driving = _drivingFlag;
            return new[]
            {
                new SignalValue("Operating_Mode", "Driving_Status", driving, driving, null, frame.Timestamp),
                new SignalValue("Operating_Mode", "Working_Status", 1 - driving, 1 - driving, null, frame.Timestamp),
            };
        }

        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    [Fact]
    public async Task TC_010_driving_then_working_transition()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var decoder = new ModeStubDecoder();
        var alarm = new AlarmEngine(Array.Empty<IAlarmRule>());
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarm);
        using var pipelineSub = pipeline.DecodedSignals.Subscribe(_ => { });

        var ctx = new TestRunnerContext(bus, pipeline.DecodedSignals, alarm.AlarmChanges, decoder);
        var runner = new TestRunner(
            new IStepExecutor[] { new ObserveBitStepExecutor(), new WaitStepExecutor() }, ctx);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bus.Inject(new CanFrame(0x0C000E00, true, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(50);
            }
        });

        // 1단계: Driving=true 확인
        var phase1 = new TestCase("TC-010a", "OperatingMode", "Driving", Array.Empty<TestStep>(),
            new TestStep[] { new ObserveBitStep("Operating_Mode", "Driving_Status", true, TimeSpan.FromMilliseconds(500)) });
        (await runner.RunAsync(phase1)).Outcome.Should().Be(TestOutcome.Passed);

        // 상태 전환
        decoder.SetWorking();

        // 2단계: Working=true 확인
        var phase2 = new TestCase("TC-010b", "OperatingMode", "Working", Array.Empty<TestStep>(),
            new TestStep[]
            {
                new WaitStep(TimeSpan.FromMilliseconds(100)),
                new ObserveBitStep("Operating_Mode", "Working_Status", true, TimeSpan.FromMilliseconds(500)),
                new ObserveBitStep("Operating_Mode", "Driving_Status", false, TimeSpan.FromMilliseconds(500)),
            });
        var result2 = await runner.RunAsync(phase2);
        cts.Cancel();

        result2.Outcome.Should().Be(TestOutcome.Passed);
        result2.StepLog.Should().HaveCount(3).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }
}
