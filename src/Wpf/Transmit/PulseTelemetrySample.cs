namespace CanMonitor.Wpf.Transmit;

public sealed record PulseTelemetrySample(
    DateTimeOffset Timestamp,
    byte Sequence,
    bool IsValid,
    bool InputHigh,
    bool PwmEnabled,
    bool WatchdogTriggered,
    int CommandedFrequencyHz,
    int HighUs,
    int PeriodUs)
{
    public double DutyPercent => IsValid && PeriodUs > 0 ? HighUs * 100.0 / PeriodUs : 0.0;
    public double MeasuredFrequencyHz => IsValid && PeriodUs > 0 ? 1_000_000.0 / PeriodUs : 0.0;
    public double FrequencyErrorHz => IsValid ? MeasuredFrequencyHz - CommandedFrequencyHz : 0.0;
}
