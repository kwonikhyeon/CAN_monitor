using CanMonitor.Wpf.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace CanMonitor.Wpf.Navigation;

public sealed class DashboardNavTarget : INavTarget
{
    public string Key => "Dashboard";
    public string Title => "Dashboard";
    public string IconGlyph => "\uE80A";
    public int Order => 10;

    public object CreateViewModel(IServiceProvider sp) => sp.GetRequiredService<DashboardViewModel>();
}
