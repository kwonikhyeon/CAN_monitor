using CanMonitor.Application.Can;
using CanMonitor.Application.Services;
using CanMonitor.Application.Testing;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    [Fact]
    public async Task AddCanMonitorApplication_resolves_all_registered_services()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var services = new ServiceCollection();
        services.AddCanMonitorApplication();
        services.AddSingleton<ICanBus>(bus);
        services.AddSingleton<ITestRunnerContext>(sp =>
            new TestRunnerContext(
                sp.GetRequiredService<ICanBus>(),
                new Subject<SignalValue>(),
                new Subject<AlarmState>(),
                new NoopDecoder()));

        await using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<CanEventHub>().Should().NotBeNull();
        provider.GetRequiredService<RawFrameStore>().Should().NotBeNull();
        provider.GetRequiredService<ManualBusStatusPublisher>().Should().NotBeNull();
        provider.GetRequiredService<IAlarmEngine>().Should().NotBeNull();
        provider.GetRequiredService<ITxScheduler>().Should().NotBeNull();
        provider.GetRequiredService<CanTransmitService>().Should().NotBeNull();
        provider.GetRequiredService<IVirtualInputService>().Should().NotBeNull();
        provider.GetRequiredService<BusLifecycleService>().Should().NotBeNull();

        var providers = provider.GetServices<IBusHeartbeatProvider>().ToArray();
        providers.Should().HaveCount(2);
        providers.Select(p => p.Name).Should().BeEquivalentTo(new[] { "EEC1", "VirtualInput" });

        provider.GetServices<IStepExecutor>().Should().HaveCount(10);
        provider.GetRequiredService<ITestRunner>().Should().NotBeNull();
    }
}
