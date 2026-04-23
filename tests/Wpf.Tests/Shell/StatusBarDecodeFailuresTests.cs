using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class StatusBarDecodeFailuresTests
{
    private sealed class FakeSessionState : ISessionState
    {
        public IObservable<ConnectionState> StateChanges => Observable.Return(ConnectionState.Disconnected);
        public IObservable<DbcFileOption?> DbcChanges => Observable.Return<DbcFileOption?>(null);
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
        public Subject<CanFrame> Unknown { get; } = new();
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Unknown;
    }

    [Fact]
    public void DecodeFailures_accumulates_unknown_frames_per_window()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var decoder = new FakeDecoder();

        var vm = new StatusBarViewModel(hub, store, alarms, session, decoder, sched);

        var f = new CanFrame(0xDEAD, true, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Rx);
        decoder.Unknown.OnNext(f);
        decoder.Unknown.OnNext(f);

        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        vm.DecodeFailures.Should().Be(2);

        decoder.Unknown.OnNext(f);
        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        vm.DecodeFailures.Should().Be(3);
    }
}
