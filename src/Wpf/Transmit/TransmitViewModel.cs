using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;

namespace CanMonitor.Wpf.Transmit;

public sealed partial class TransmitViewModel : ObservableObject, IDisposable
{
    private const uint ControlCanId = 0x600;
    private const byte CommandOff = 0x00;
    private const byte CommandOn = 0x01;
    private const byte CommandToggle = 0x02;
    private const uint PcHeartbeatCanId = 0x610;
    private const uint TeensyHeartbeatCanId = 0x611;
    private const uint Pwm1TelemetryCanId = 0x621;
    private const uint Pwm2TelemetryCanId = 0x622;
    private const byte HeartbeatMagic = 0xA5;
    private static readonly TimeSpan HeartbeatPeriod = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMilliseconds(3000);

    private readonly ICanFrameTransmitter _transmitter;
    private readonly Dispatcher _dispatcher;
    private readonly IDisposable _stateSubscription;
    private readonly IDisposable _frameSubscription;
    private readonly DispatcherTimer _heartbeatTimer;
    private DateTimeOffset? _lastTeensyHeartbeatAt;
    private uint _pcHeartbeatCount;
    private uint _teensyHeartbeatCount;

    [ObservableProperty] private ConnectionState _connectionState;
    [ObservableProperty] private string _lastCommand = "None";
    [ObservableProperty] private string _lastSentAt = "-";
    [ObservableProperty] private string _pcHeartbeatStatus = "Stopped";
    [ObservableProperty] private string _teensyHeartbeatStatus = "No heartbeat";
    [ObservableProperty] private string _lastTeensyHeartbeatAtText = "-";
    [ObservableProperty] private double _timeWindowMilliseconds = 20;
    [ObservableProperty] private string? _errorMessage;

    public TransmitViewModel(ICanFrameTransmitter transmitter)
    {
        _transmitter = transmitter;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _connectionState = transmitter.State;
        _stateSubscription = transmitter.StateChanges.Subscribe(state =>
        {
            ConnectionState = state;
            SendOnCommand.NotifyCanExecuteChanged();
            SendOffCommand.NotifyCanExecuteChanged();
            SendToggleCommand.NotifyCanExecuteChanged();
        });
        _frameSubscription = transmitter.Frames.Subscribe(HandleFrame);
        _heartbeatTimer = new DispatcherTimer
        {
            Interval = HeartbeatPeriod
        };
        _heartbeatTimer.Tick += async (_, _) => await HeartbeatTickAsync();
        if (ConnectionState == ConnectionState.Connected)
            _heartbeatTimer.Start();
    }

    public bool CanSend => ConnectionState == ConnectionState.Connected;
    public string ControlIdText => "0x600";
    public string ProtocolText => "data[0]: 00=OFF, 01=ON, 02=TOGGLE";
    public string HeartbeatProtocolText => "PC -> 0x610#A5 every 1s, Teensy -> 0x611#A5";
    public string TelemetryProtocolText => "PWM telemetry: 0x621=A0/PWM2, 0x622=A1/PWM3";
    public string TimeWindowText => $"{TimeWindowMilliseconds:0} ms";
    public PulseTelemetryChannelViewModel Channel1 { get; } = new("A0 -> PWM2", Pwm1TelemetryCanId);
    public PulseTelemetryChannelViewModel Channel2 { get; } = new("A1 -> PWM3", Pwm2TelemetryCanId);

    partial void OnTimeWindowMillisecondsChanged(double value)
    {
        OnPropertyChanged(nameof(TimeWindowText));
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendOnAsync() => SendControlCommandAsync("ON", CommandOn);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendOffAsync() => SendControlCommandAsync("OFF", CommandOff);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendToggleAsync() => SendControlCommandAsync("TOGGLE", CommandToggle);

    private async Task SendControlCommandAsync(string label, byte command)
    {
        try
        {
            var frame = new CanFrame(
                ControlCanId,
                IsExtended: false,
                new byte[] { command },
                DateTimeOffset.UtcNow,
                CanDirection.Tx);

            await _transmitter.SendFrameAsync(frame);
            LastCommand = $"{label} ({ControlCanId:X3}#{command:X2})";
            LastSentAt = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    partial void OnConnectionStateChanged(ConnectionState value)
    {
        OnPropertyChanged(nameof(CanSend));
        if (value == ConnectionState.Connected)
        {
            _heartbeatTimer.Start();
        }
        else
        {
            _heartbeatTimer.Stop();
            PcHeartbeatStatus = "Stopped";
            TeensyHeartbeatStatus = "No heartbeat";
            LastTeensyHeartbeatAtText = "-";
            _lastTeensyHeartbeatAt = null;
            Channel1.Reset();
            Channel2.Reset();
        }
    }

    private async Task HeartbeatTickAsync()
    {
        if (ConnectionState != ConnectionState.Connected)
            return;

        try
        {
            await _transmitter.SendFrameAsync(new CanFrame(
                PcHeartbeatCanId,
                IsExtended: false,
                new byte[] { HeartbeatMagic },
                DateTimeOffset.UtcNow,
                CanDirection.Tx));
            _pcHeartbeatCount++;
            PcHeartbeatStatus = $"TX ok ({_pcHeartbeatCount})";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            PcHeartbeatStatus = "TX failed";
            ErrorMessage = ex.Message;
        }

        if (_lastTeensyHeartbeatAt is null)
        {
            TeensyHeartbeatStatus = "Waiting";
            return;
        }

        var age = DateTimeOffset.UtcNow - _lastTeensyHeartbeatAt.Value;
        TeensyHeartbeatStatus = age <= HeartbeatTimeout
            ? $"OK ({(int)age.TotalMilliseconds} ms ago, rx {_teensyHeartbeatCount})"
            : $"TIMEOUT ({(int)age.TotalMilliseconds} ms)";
    }

    private void HandleFrame(CanFrame frame)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => HandleFrame(frame));
            return;
        }

        if (frame.Direction != CanDirection.Rx ||
            frame.IsExtended)
        {
            return;
        }

        if (frame.Id == TeensyHeartbeatCanId)
        {
            if (frame.Data.Length < 1 || frame.Data.Span[0] != HeartbeatMagic)
                return;

            _lastTeensyHeartbeatAt = DateTimeOffset.UtcNow;
            _teensyHeartbeatCount++;
            LastTeensyHeartbeatAtText = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
            TeensyHeartbeatStatus = $"OK (rx {_teensyHeartbeatCount})";
            return;
        }

        if (frame.Id == Pwm1TelemetryCanId)
        {
            TryAddPulseTelemetry(Channel1, frame);
            return;
        }

        if (frame.Id == Pwm2TelemetryCanId)
        {
            TryAddPulseTelemetry(Channel2, frame);
        }
    }

    private static void TryAddPulseTelemetry(PulseTelemetryChannelViewModel channel, CanFrame frame)
    {
        var data = frame.Data.Span;
        if (data.Length < 8)
            return;

        var flags = data[1];
        channel.AddSample(new PulseTelemetrySample(
            DateTimeOffset.UtcNow,
            data[0],
            IsValid: (flags & 0x01) != 0,
            InputHigh: (flags & 0x02) != 0,
            PwmEnabled: (flags & 0x04) != 0,
            WatchdogTriggered: (flags & 0x08) != 0,
            AdcRaw: ReadUInt16Le(data, 2),
            HighUs: ReadUInt16Le(data, 4),
            PeriodUs: ReadUInt16Le(data, 6)));
    }

    private static int ReadUInt16Le(ReadOnlySpan<byte> data, int offset)
    {
        return data[offset] | (data[offset + 1] << 8);
    }

    public void Dispose()
    {
        _heartbeatTimer.Stop();
        _stateSubscription.Dispose();
        _frameSubscription.Dispose();
    }
}
