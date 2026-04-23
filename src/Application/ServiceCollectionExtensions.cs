using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Services;
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

        services.AddSingleton<IAlarmEngine>(_ => new AlarmEngine(Array.Empty<IAlarmRule>()));
        services.AddSingleton<CanTransmitService>();

        services.AddSingleton<IVirtualInputService, VirtualInputService>();

        services.AddSingleton<Eec1HeartbeatProvider>();
        services.AddSingleton<VirtualInputHeartbeat>();
        services.AddSingleton<IBusHeartbeatProvider>(sp => sp.GetRequiredService<Eec1HeartbeatProvider>());
        services.AddSingleton<IBusHeartbeatProvider>(sp => sp.GetRequiredService<VirtualInputHeartbeat>());

        services.AddSingleton<IStepExecutor, WaitStepExecutor>();
        services.AddSingleton<IStepExecutor, SendCanFrameStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveSignalStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveBitStepExecutor>();
        services.AddSingleton<IStepExecutor, AssertFrameRateStepExecutor>();
        services.AddSingleton<IStepExecutor, ManualConfirmStepExecutor>();
        services.AddSingleton<IStepExecutor, SetHeartbeatStepExecutor>();
        services.AddSingleton<IStepExecutor, SetVirtualInputStepExecutor>();
        services.AddSingleton<IStepExecutor, EnterSimulationModeStepExecutor>();
        services.AddSingleton<IStepExecutor, ExitSimulationModeStepExecutor>();

        services.AddTransient<ITestRunner, TestRunner>();
        return services;
    }
}
