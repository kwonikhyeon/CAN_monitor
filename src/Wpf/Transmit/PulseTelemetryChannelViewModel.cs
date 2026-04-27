using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Transmit;

public sealed partial class PulseTelemetryChannelViewModel : ObservableObject
{
    private const int MaxSamples = 600;

    [ObservableProperty] private string _lastSeenText = "-";
    [ObservableProperty] private string _metricsText = "waiting for telemetry";
    [ObservableProperty] private string _stateText = "No data";

    public PulseTelemetryChannelViewModel(string name, uint frameId)
    {
        Name = name;
        FrameId = frameId;
        FrameIdText = $"0x{frameId:X3}";
    }

    public string Name { get; }
    public uint FrameId { get; }
    public string FrameIdText { get; }
    public ObservableCollection<PulseTelemetrySample> Samples { get; } = new();

    public void AddSample(PulseTelemetrySample sample)
    {
        Samples.Add(sample);
        while (Samples.Count > MaxSamples)
            Samples.RemoveAt(0);

        LastSeenText = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        StateText = sample.IsValid
            ? sample.PwmEnabled ? "PWM active" : "PWM measured while disabled"
            : sample.InputHigh ? "steady HIGH" : "steady LOW";

        MetricsText = sample.IsValid
            ? $"ADC {sample.AdcRaw:0000} | {sample.Voltage:0.000} V | {sample.FrequencyHz:0.0} Hz | duty {sample.DutyPercent:0.00}%"
            : $"ADC {sample.AdcRaw:0000} | {sample.Voltage:0.000} V | no valid pulses";
    }

    public void Reset()
    {
        Samples.Clear();
        LastSeenText = "-";
        MetricsText = "waiting for telemetry";
        StateText = "No data";
    }
}
