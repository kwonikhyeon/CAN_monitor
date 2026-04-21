namespace CanMonitor.Core.Testing;

public abstract record TestStep(string Type);

public sealed record WaitStep(TimeSpan Duration) : TestStep("Wait");

public sealed record ObserveSignalStep(
    string Message,
    string Signal,
    double Expected,
    TimeSpan Within,
    double Tolerance) : TestStep("ObserveSignal");

public sealed record ObserveBitStep(
    string Message,
    string Signal,
    bool Expected,
    TimeSpan Within) : TestStep("ObserveBit");

public sealed record SendCanFrameStep(
    uint Id,
    bool IsExtended,
    byte[] Data) : TestStep("SendCanFrame");

public sealed record AssertFrameRateStep(
    uint CanId,
    double HzExpected,
    double TolerancePct) : TestStep("AssertFrameRate");

public sealed record ManualConfirmStep(string Instruction) : TestStep("ManualConfirm");

public sealed record SetVirtualInputStep(
    string? GearLever = null,
    string? RangeShift = null,
    bool?   TemperatureSwitch = null,
    double? ClutchPedalPercent = null,
    double? WheelSpeedKph = null,
    double? SpeedSensor1Rpm = null,
    double? SpeedSensor2Rpm = null,
    bool?   PtoSwitch = null,
    bool?   FourWdSwitch = null,
    bool?   InchingSwitch = null,
    bool?   ParkingSwitch = null) : TestStep("SetVirtualInput");

public sealed record SetHeartbeatStep(string Name, bool Enabled) : TestStep("SetHeartbeat");

public sealed record EnterSimulationModeStep() : TestStep("EnterSimulationMode");

public sealed record ExitSimulationModeStep() : TestStep("ExitSimulationMode");
