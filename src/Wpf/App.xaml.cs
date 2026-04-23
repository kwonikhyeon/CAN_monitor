using System.Reactive.Concurrency;
using System.Windows;
using System.Windows.Threading;
using CanMonitor.Application;
using CanMonitor.Dbc;
using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Dashboard.Widgets;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Navigation;
using CanMonitor.Wpf.Shell;
using CanMonitor.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WpfApplication = System.Windows.Application;

namespace CanMonitor.Wpf;

public partial class App : WpfApplication
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);
        _host = builder.Build();
        await _host.StartAsync();

        var shell = _host.Services.GetRequiredService<ShellWindow>();
        shell.DataContext = _host.Services.GetRequiredService<ShellViewModel>();
        shell.Show();

        _ = _host.Services.GetRequiredService<SessionViewModel>().InitializeAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCanMonitorApplication()
            .AddSingleton<IScheduler>(_ => new DispatcherScheduler(WpfApplication.Current.Dispatcher))
            .AddSingleton<IDbcProvider, DbcParserLibProvider>()
            .AddSingleton<Wpf.Infrastructure.ICanBusFactory, CanBusFactory>()
            .AddSingleton<ShellWindow>()
            .AddSingleton<ShellViewModel>()
            .AddSingleton<SessionViewModel>()
            .AddSingleton<ISessionState>(sp => sp.GetRequiredService<SessionViewModel>())
            .AddSingleton<StatusBarViewModel>()
            .AddSingleton<DashboardViewModel>()
            .AddSingleton<INavTarget, DashboardNavTarget>()                                         // Order=10
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Raw",       "\uE8FD", "Raw Log",         20))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Transmit",  "\uE724", "Transmit",        30))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Test",      "\uE90F", "Test Runner",     40))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("DBC",       "\uE70F", "DBC Editor",      50))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Input",     "\uE7FC", "Input Emulation", 60))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Heartbeat", "\uEB51", "Heartbeat",       70))
            .AddSingleton<IDashboardWidget>(_ => new PlaceholderWidget("Trend Chart",   280, row: 0, column: 0))
            .AddSingleton<IDashboardWidget>(_ => new PlaceholderWidget("Signal Values", 280, row: 0, column: 1))
            .AddSingleton<IDashboardWidget>(_ => new PlaceholderWidget("Alarm Panel",   220, row: 1, column: 0, columnSpan: 2));
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                var session = _host.Services.GetRequiredService<SessionViewModel>();
                await session.DisconnectAsync();
            }
            finally
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        base.OnExit(e);
    }
}
