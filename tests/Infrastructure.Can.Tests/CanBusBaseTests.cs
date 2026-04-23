using System.Reactive.Linq;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Infrastructure.Can.Tests;

public sealed class CanBusBaseTests
{
    private sealed class TestBus : CanBusBase
    {
        public int OpenCount  { get; private set; }
        public int CloseCount { get; private set; }
        public List<CanFrame> SentToDriver { get; } = new();

        public override string Name => "Test";

        public void InjectRxPublic(CanFrame frame) => PublishRx(frame);

        protected override Task OpenDriverAsync(CanBusOptions options, CancellationToken ct)
        {
            OpenCount++;
            return Task.CompletedTask;
        }

        protected override Task StopDriverAsync()
        {
            CloseCount++;
            return Task.CompletedTask;
        }

        protected override Task SendDriverAsync(CanFrame frame, CancellationToken ct)
        {
            SentToDriver.Add(frame);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task OpenAsync_sets_IsOpen_and_invokes_driver()
    {
        var bus = new TestBus();
        bus.IsOpen.Should().BeFalse();

        await bus.OpenAsync(new CanBusOptions());
        bus.IsOpen.Should().BeTrue();
        bus.OpenCount.Should().Be(1);
    }

    [Fact]
    public async Task OpenAsync_twice_throws()
    {
        var bus = new TestBus();
        await bus.OpenAsync(new CanBusOptions());
        Func<Task> act = () => bus.OpenAsync(new CanBusOptions());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishRx_copies_payload_into_independent_buffer()
    {
        var bus = new TestBus();
        await bus.OpenAsync(new CanBusOptions());

        var captured = new List<CanFrame>();
        using var _ = bus.Frames.Subscribe(captured.Add);

        var mutableBuffer = new byte[] { 0x11, 0x22, 0x33 };
        bus.InjectRxPublic(new CanFrame(0x100, false, mutableBuffer, DateTimeOffset.UtcNow, CanDirection.Rx));

        // 발행 이후 호출자 버퍼를 변조한다 — 구독자는 변경되지 않은 바이트를 관찰해야 한다.
        mutableBuffer[0] = 0xFF;

        captured.Should().ContainSingle();
        captured[0].Data.ToArray().Should().Equal(0x11, 0x22, 0x33);
        captured[0].Direction.Should().Be(CanDirection.Rx);
    }

    [Fact]
    public async Task SendAsync_echoes_frame_as_Tx()
    {
        var bus = new TestBus();
        await bus.OpenAsync(new CanBusOptions());

        var captured = new List<CanFrame>();
        using var _ = bus.Frames.Subscribe(captured.Add);

        var outbound = new CanFrame(0x200, false, new byte[] { 0xAA }, DateTimeOffset.UtcNow, CanDirection.Tx);
        await bus.SendAsync(outbound);

        bus.SentToDriver.Should().ContainSingle().Which.Id.Should().Be(0x200u);
        captured.Should().ContainSingle(f => f.Direction == CanDirection.Tx && f.Id == 0x200u);
    }

    [Fact]
    public async Task CloseAsync_runs_cancel_stop_complete_sequence_once()
    {
        var bus = new TestBus();
        await bus.OpenAsync(new CanBusOptions());

        var completed = false;
        using var _ = bus.Frames.Subscribe(_ => { }, () => completed = true);

        await bus.CloseAsync();
        await bus.CloseAsync();                               // 멱등

        bus.CloseCount.Should().Be(1);
        completed.Should().BeTrue();
        bus.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_closes_then_releases_resources()
    {
        var bus = new TestBus();
        await bus.OpenAsync(new CanBusOptions());
        await bus.DisposeAsync();

        bus.IsOpen.Should().BeFalse();
        bus.CloseCount.Should().Be(1);
    }
}
