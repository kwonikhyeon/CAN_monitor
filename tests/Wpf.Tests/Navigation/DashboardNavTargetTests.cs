using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Navigation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanMonitor.Wpf.Tests.Navigation;

public class DashboardNavTargetTests
{
    [Fact]
    public void Properties_are_fixed()
    {
        var target = new DashboardNavTarget();
        target.Key.Should().Be("Dashboard");
        target.Title.Should().Be("Dashboard");
        target.IconGlyph.Should().Be("\uE80A");
        target.Order.Should().Be(10);
    }

    [Fact]
    public void CreateViewModel_resolves_DashboardViewModel_from_sp()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DashboardViewModel(Array.Empty<IDashboardWidget>()));
        var sp = services.BuildServiceProvider();

        var target = new DashboardNavTarget();
        var vm = target.CreateViewModel(sp);

        vm.Should().BeOfType<DashboardViewModel>();
    }
}
