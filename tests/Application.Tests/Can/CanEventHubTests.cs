using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class CanEventHubTests
{
    [Fact]
    public async Task Subscriber_created_before_Attach_receives_frames_after_Attach()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var hub = new CanEventHub();

        var received = new List<CanFrame>();
        using var _ = hub.Frames.Subscribe(received.Add);

        using var __ = hub.Attach(
            bus,
            signals: new Subject<SignalValue>(),
            alarms: new Subject<AlarmState>(),
            busStatus: new Subject<BusStatusChange>());

        bus.Inject(new CanFrame(0x100, false, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        received.Should().ContainSingle().Which.Id.Should().Be(0x100u);
    }

    [Fact]
    public void Upstream_OnCompleted_does_not_propagate_to_downstream()
    {
        var hub = new CanEventHub();
        var upstreamFrames = new Subject<CanFrame>();

        bool downstreamCompleted = false;
        using var _ = hub.Frames.Subscribe(_ => { }, () => downstreamCompleted = true);

        using var __ = hub.AttachRawStreams(
            upstreamFrames,
            signals: new Subject<SignalValue>(),
            alarms: new Subject<AlarmState>(),
            busStatus: new Subject<BusStatusChange>());

        upstreamFrames.OnCompleted();

        downstreamCompleted.Should().BeFalse("hub은 프로세스 수명 동안 완료되면 안 된다");
    }

    [Fact]
    public void Re_Attach_replaces_upstream_without_completing_downstream()
    {
        var hub = new CanEventHub();
        var firstUpstream = new Subject<CanFrame>();
        var secondUpstream = new Subject<CanFrame>();

        var received = new List<uint>();
        using var _ = hub.Frames.Subscribe(f => received.Add(f.Id));

        var firstBinding = hub.AttachRawStreams(
            firstUpstream,
            new Subject<SignalValue>(), new Subject<AlarmState>(), new Subject<BusStatusChange>());

        firstUpstream.OnNext(new CanFrame(0x1, false, Array.Empty<byte>(), DateTimeOffset.UtcNow, CanDirection.Rx));
        firstBinding.Dispose();

        using var __ = hub.AttachRawStreams(
            secondUpstream,
            new Subject<SignalValue>(), new Subject<AlarmState>(), new Subject<BusStatusChange>());

        secondUpstream.OnNext(new CanFrame(0x2, false, Array.Empty<byte>(), DateTimeOffset.UtcNow, CanDirection.Rx));
        firstUpstream.OnNext(new CanFrame(0x999, false, Array.Empty<byte>(), DateTimeOffset.UtcNow, CanDirection.Rx));

        received.Should().Equal(new uint[] { 0x1, 0x2 });
    }
}
