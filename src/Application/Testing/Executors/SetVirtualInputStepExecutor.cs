using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class SetVirtualInputStepExecutor : IStepExecutor<SetVirtualInputStep>
{
    private readonly IVirtualInputService _service;

    public SetVirtualInputStepExecutor(IVirtualInputService service)
    {
        _service = service;
    }

    public Type StepType => typeof(SetVirtualInputStep);

    public Task<StepOutcome> ExecuteAsync(SetVirtualInputStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var cur = _service.Current;

        if (!TryParseGear(step.GearLever, cur.GearLever, out var gear))
            return Task.FromResult(StepOutcome.Failed);
        if (!TryParseRange(step.RangeShift, cur.RangeShift, out var range))
            return Task.FromResult(StepOutcome.Failed);

        var next = cur with
        {
            GearLever          = gear,
            RangeShift         = range,
            TemperatureSwitch  = step.TemperatureSwitch  ?? cur.TemperatureSwitch,
            ClutchPedalPercent = step.ClutchPedalPercent ?? cur.ClutchPedalPercent,
            WheelSpeedKph      = step.WheelSpeedKph      ?? cur.WheelSpeedKph,
            SpeedSensor1Rpm    = step.SpeedSensor1Rpm    ?? cur.SpeedSensor1Rpm,
            SpeedSensor2Rpm    = step.SpeedSensor2Rpm    ?? cur.SpeedSensor2Rpm,
            PtoSwitch          = step.PtoSwitch          ?? cur.PtoSwitch,
            FourWdSwitch       = step.FourWdSwitch       ?? cur.FourWdSwitch,
            InchingSwitch      = step.InchingSwitch      ?? cur.InchingSwitch,
            ParkingSwitch      = step.ParkingSwitch      ?? cur.ParkingSwitch,
        };
        _service.Update(next);
        return Task.FromResult(StepOutcome.Passed);
    }

    private static bool TryParseGear(string? input, GearLever fallback, out GearLever result)
    {
        result = fallback;
        if (input is null) return true;
        switch (input.Trim().ToUpperInvariant())
        {
            case "NONE":    result = GearLever.None;    return true;
            case "NEUTRAL":
            case "N":       result = GearLever.Neutral; return true;
            case "FORWARD":
            case "F":       result = GearLever.Forward; return true;
            case "REVERSE":
            case "R":       result = GearLever.Reverse; return true;
            default: return false;
        }
    }

    private static bool TryParseRange(string? input, RangeShift fallback, out RangeShift result)
    {
        result = fallback;
        if (input is null) return true;
        switch (input.Trim().ToUpperInvariant())
        {
            case "NONE":   result = RangeShift.None;   return true;
            case "FIRST":
            case "1":      result = RangeShift.First;  return true;
            case "SECOND":
            case "2":      result = RangeShift.Second; return true;
            case "THIRD":
            case "3":      result = RangeShift.Third;  return true;
            default: return false;
        }
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((SetVirtualInputStep)step, context, ct);
}
