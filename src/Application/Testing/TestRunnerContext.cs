using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Testing;

public sealed record TestRunnerContext(
    ICanBus Bus,
    IObservable<SignalValue> Signals,
    IObservable<AlarmState> Alarms,
    ISignalDecoder Decoder) : ITestRunnerContext;
