using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Integration.Tests.TestCases;

public sealed class Tc001FrameRateTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    [Fact]
    public async Task TC_001_100Hz_frame_passes_rate_assertion()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var runner = new TestRunner(new IStepExecutor[] { new AssertFrameRateStepExecutor() }, ctx);

        var testCase = new TestCase(
            "TC-001", "Frame", "주기/누락",
            Prerequisites: Array.Empty<TestStep>(),
            Steps: new TestStep[] { new AssertFrameRateStep(0x0CF00400, 100, TolerancePct: 20) });

        // 결정적 주입: 러너를 먼저 시작 (executor 가 첫 await 이전에 동기적으로 구독),
        // 이후 100 프레임을 동기적으로 주입해 모두 첫 Buffer(1s) 윈도우에 떨어지도록 한다.
        // Windows 타이머 분해능에 의한 flaky 문제 회피 (AssertFrameRateStepExecutorTests 수정 참조).
        var runTask = runner.RunAsync(testCase);
        for (int i = 0; i < 100; i++)
            bus.Inject(new CanFrame(0x0CF00400, true, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        var result = await runTask;

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().ContainSingle().Which.Outcome.Should().Be(StepOutcome.Passed);
    }
}
