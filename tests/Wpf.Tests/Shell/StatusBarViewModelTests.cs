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

public class StatusBarViewModelTests
{
    private sealed class FakeSessionState : ISessionState
    {
        public readonly BehaviorSubject<ConnectionState> State = new(ConnectionState.Disconnected);
        public readonly BehaviorSubject<DbcFileOption?> Dbc = new(null);
        public IObservable<ConnectionState> StateChanges => State;
        public IObservable<DbcFileOption?> DbcChanges => Dbc;
    }

    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public readonly Subject<AlarmState> Changes = new();
        public IObservable<AlarmState> AlarmChanges => Changes;
        public IReadOnlyCollection<AlarmState> CurrentAlarms { get; set; } = Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) { }
    }

    private static CanFrame MakeFrame(CanDirection dir)
        => new(0x100, false, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow, dir);

    [Fact]
    public void RxPerSecond_counts_Rx_frames_over_one_second()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var frames = new Subject<CanFrame>();
        hub.AttachRawStreams(frames, Observable.Never<SignalValue>(),
            Observable.Never<AlarmState>(), Observable.Never<BusStatusChange>());
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();

        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        frames.OnNext(MakeFrame(CanDirection.Rx));
        frames.OnNext(MakeFrame(CanDirection.Rx));
        frames.OnNext(MakeFrame(CanDirection.Tx));

        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        vm.RxPerSecond.Should().Be(2);
        vm.TxPerSecond.Should().Be(1);
    }

    [Fact]
    public void DroppedFrames_reflects_store_counter_after_sample()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore(capacity: 1);
        store.Record(MakeFrame(CanDirection.Rx));
        store.Record(MakeFrame(CanDirection.Rx));  // 1 건 드롭
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();

        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        vm.DroppedFrames.Should().Be(1);
    }

    [Fact]
    public void ActiveAlarms_counts_Active_state_transitions()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        alarms.Changes.OnNext(new AlarmState("A1", AlarmSeverity.Warning, "x", true, DateTimeOffset.UtcNow));
        alarms.Changes.OnNext(new AlarmState("A2", AlarmSeverity.Error, "y", true, DateTimeOffset.UtcNow));
        alarms.Changes.OnNext(new AlarmState("A1", AlarmSeverity.Warning, "x", false, DateTimeOffset.UtcNow));

        sched.AdvanceBy(1);
        vm.ActiveAlarms.Should().Be(1);
    }

    [Fact]
    public void Session_updates_on_state_change()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        session.State.OnNext(ConnectionState.Connected);
        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        vm.Session.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public void DbcFileLabel_is_empty_when_no_selection()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        session.Dbc.OnNext(new DbcFileOption("x/y.dbc", "120HP_NoPto.dbc", DbcSource.Confirmed));
        sched.AdvanceBy(1);

        vm.DbcFileLabel.Should().Be("120HP_NoPto.dbc");
    }
}
