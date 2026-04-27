namespace CanMonitor.Wpf.Transmit;

public sealed record PulseTelemetrySample(
    DateTimeOffset Timestamp,
    byte Sequence,
    bool IsValid,
    bool InputHigh,
    bool PwmEnabled,
    bool WatchdogTriggered,
    int AdcRaw,
    int HighUs,
    int PeriodUs)
{
    public double Voltage => AdcRaw * 3.3 / 4095.0;
    public double DutyPercent => IsValid && PeriodUs > 0 ? HighUs * 100.0 / PeriodUs : 0.0;
    public double FrequencyHz => IsValid && PeriodUs > 0 ? 1_000_000.0 / PeriodUs : 0.0;
}
