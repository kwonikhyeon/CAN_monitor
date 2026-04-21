namespace CanMonitor.Core.Models;

public enum GearLever { None = 0, Neutral = 1, Forward = 2, Reverse = 3 }
public enum RangeShift { None = 0, First = 1, Second = 2, Third = 3 }

public sealed record VirtualInputState(
    GearLever  GearLever          = GearLever.None,
    RangeShift RangeShift         = RangeShift.None,
    bool       TemperatureSwitch  = false,
    double     ClutchPedalPercent = 0.0,
    double     WheelSpeedKph      = 0.0,
    double     SpeedSensor1Rpm    = 0.0,
    double     SpeedSensor2Rpm    = 0.0,
    bool       PtoSwitch          = false,
    bool       FourWdSwitch       = false,
    bool       InchingSwitch      = false,
    bool       ParkingSwitch      = false);
