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
        sut.SendChannel1DutyCommand.CanExecute(null).Should().BeFalse();
        sut.SendChannel2DutyCommand.CanExecute(null).Should().BeFalse();
        sut.SendBothDutyCommand.CanExecute(null).Should().BeFalse();
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

    [Fact]
    public async Task SendChannel1Duty_sends_0x600_10_mask_01_raw_duty()
    {
        var transmitter = new FakeTransmitter();
        transmitter.SetState(ConnectionState.Connected);
        var sut = new TransmitViewModel(transmitter)
        {
            Channel1DutyPercent = 25
        };

        await sut.SendChannel1DutyCommand.ExecuteAsync(null);

        transmitter.Sent.Should().ContainSingle();
        transmitter.Sent[0].Id.Should().Be(0x600);
        transmitter.Sent[0].IsExtended.Should().BeFalse();
        transmitter.Sent[0].Data.ToArray().Should().Equal(0x10, 0x01, 0x00, 0x04);
    }

    [Fact]
    public async Task SendBothDuty_sends_one_frame_per_channel()
    {
        var transmitter = new FakeTransmitter();
        transmitter.SetState(ConnectionState.Connected);
        var sut = new TransmitViewModel(transmitter)
        {
            Channel1DutyPercent = 50,
            Channel2DutyPercent = 100
        };

        await sut.SendBothDutyCommand.ExecuteAsync(null);

        transmitter.Sent.Should().HaveCount(2);
        transmitter.Sent[0].Data.ToArray().Should().Equal(0x10, 0x01, 0x00, 0x08);
        transmitter.Sent[1].Data.ToArray().Should().Equal(0x10, 0x02, 0xFF, 0x0F);
    }

    [Fact]
    public void Pwm1TelemetryFrame_updates_channel1_samples()
    {
        var transmitter = new FakeTransmitter();
        var sut = new TransmitViewModel(transmitter);

        transmitter.FramesSubject.OnNext(new CanFrame(
            0x621,
            IsExtended: false,
            new byte[] { 0x12, 0x07, 0x20, 0x03, 0xE8, 0x03, 0xE2, 0x04 },
            DateTimeOffset.UtcNow,
            CanDirection.Rx));

        sut.Channel1.Samples.Should().ContainSingle();
        var sample = sut.Channel1.Samples[0];
        sample.Sequence.Should().Be(0x12);
        sample.IsValid.Should().BeTrue();
        sample.InputHigh.Should().BeTrue();
        sample.PwmEnabled.Should().BeTrue();
        sample.CommandedFrequencyHz.Should().Be(800);
        sample.HighUs.Should().Be(1000);
        sample.PeriodUs.Should().Be(1250);
        sample.MeasuredFrequencyHz.Should().BeApproximately(800.0, 0.01);
        sample.FrequencyErrorHz.Should().BeApproximately(0.0, 0.01);
        sample.DutyPercent.Should().BeApproximately(80.0, 0.01);
    }

    [Fact]
    public void Pwm2TelemetryFrame_updates_channel2_samples()
    {
        var transmitter = new FakeTransmitter();
        var sut = new TransmitViewModel(transmitter);

        transmitter.FramesSubject.OnNext(new CanFrame(
            0x622,
            IsExtended: false,
            new byte[] { 0x13, 0x00, 0xB8, 0x0B, 0x00, 0x00, 0x00, 0x00 },
            DateTimeOffset.UtcNow,
            CanDirection.Rx));

        sut.Channel2.Samples.Should().ContainSingle();
        sut.Channel2.Samples[0].IsValid.Should().BeFalse();
        sut.Channel2.Samples[0].CommandedFrequencyHz.Should().Be(3000);
    }
}
