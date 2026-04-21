using CanMonitor.Core.Testing;

namespace CanMonitor.Core.Abstractions;

public interface IStepExecutor
{
    Type StepType { get; }
    Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct);
}

public interface IStepExecutor<TStep> : IStepExecutor where TStep : TestStep
{
    Task<StepOutcome> ExecuteAsync(TStep step, ITestRunnerContext context, CancellationToken ct);
}

public interface ITestRunnerContext
{
    ICanBus Bus { get; }
    IObservable<Models.SignalValue> Signals { get; }
    IObservable<Models.AlarmState> Alarms { get; }
    ISignalDecoder Decoder { get; }
}
