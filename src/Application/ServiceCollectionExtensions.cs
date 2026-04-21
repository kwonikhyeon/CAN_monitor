using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanMonitor.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanMonitorApplication(this IServiceCollection services)
    {
        services.AddSingleton<CanEventHub>();
        services.AddSingleton<RawFrameStore>();
        services.AddSingleton<ManualBusStatusPublisher>();

        services.AddSingleton<IAlarmEngine>(_ => new AlarmEngine(AlarmRuleFactory.CreatePhase2aRules()));
        services.AddSingleton<ITxScheduler, TxScheduler>();
        services.AddSingleton<CanTransmitService>();

        services.AddSingleton<IStepExecutor, WaitStepExecutor>();
        services.AddSingleton<IStepExecutor, SendCanFrameStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveSignalStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveBitStepExecutor>();
        services.AddSingleton<IStepExecutor, AssertFrameRateStepExecutor>();
        services.AddSingleton<IStepExecutor, ManualConfirmStepExecutor>();

        services.AddTransient<ITestRunner, TestRunner>();
        return services;
    }
}
