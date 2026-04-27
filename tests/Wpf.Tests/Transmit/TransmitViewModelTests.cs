using System.Reactive.Subjects;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Transmit;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Transmit;

public sealed class TransmitViewModelTests
{
    private sealed class FakeTransmitter : ICanFrameTransmitter
    {
        private readonly BehaviorSubject<ConnectionState> _state = new(ConnectionState.Disconnected);

        public List<CanFrame> Sent { get; } = new();
        public Subject<CanFrame> FramesSubject { get; } = new();
        public ConnectionState State => _state.Value;
        public IObservable<ConnectionState> StateChanges => _state;
        public IObservable<CanFrame> Frames => FramesSubject;

        public Task SendFrameAsync(CanFrame frame, CancellationToken ct = default)
        {
            Sent.Add(frame);
            return Task.CompletedTask;
        }

        public void SetState(ConnectionState state) => _state.OnNext(state);
    }

    [Fact]
    public void Commands_are_disabled_when_disconnected()
    {
        var transmitter = new FakeTransmitter();
        var sut = new TransmitViewModel(transmitter);

        sut.SendOnCommand.CanExecute(null).Should().BeFalse();
        sut.SendOffCommand.CanExecute(null).Should().BeFalse();
        sut.SendToggleCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SendOn_sends_0x600_01()
    {
        var transmitter = new FakeTransmitter();
        transmitter.SetState(ConnectionState.Connected);
        var sut = new TransmitViewModel(transmitter);

        await sut.SendOnCommand.ExecuteAsync(null);

        transmitter.Sent.Should().ContainSingle();
        transmitter.Sent[0].Id.Should().Be(0x600);
        transmitter.Sent[0].IsExtended.Should().BeFalse();
        transmitter.Sent[0].Data.ToArray().Should().Equal(0x01);
    }

    [Fact]
    public async Task SendOff_sends_0x600_00()
    {
        var transmitter = new FakeTransmitter();
        transmitter.SetState(ConnectionState.Connected);
        var sut = new TransmitViewModel(transmitter);

        await sut.SendOffCommand.ExecuteAsync(null);

        transmitter.Sent.Should().ContainSingle();
        transmitter.Sent[0].Data.ToArray().Should().Equal(0x00);
    }

    [Fact]
    public async Task SendToggle_sends_0x600_02()
    {
        var transmitter = new FakeTransmitter();
        transmitter.SetState(ConnectionState.Connected);
        var sut = new TransmitViewModel(transmitter);

        await sut.SendToggleCommand.ExecuteAsync(null);

        transmitter.Sent.Should().ContainSingle();
        transmitter.Sent[0].Data.ToArray().Should().Equal(0x02);
    }
}
