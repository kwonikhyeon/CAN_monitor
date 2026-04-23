using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Dashboard.Widgets;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Dashboard;

public class DashboardViewModelTests
{
    [Fact]
    public void Widgets_preserves_registration_order()
    {
        var a = new PlaceholderWidget("A", 100);
        var b = new PlaceholderWidget("B", 100);
        var c = new PlaceholderWidget("C", 100);

        var vm = new DashboardViewModel(new[] { a, b, c });

        vm.Widgets.Should().Equal(a, b, c);
    }

    [Fact]
    public void Widgets_is_empty_when_no_registrations()
    {
        var vm = new DashboardViewModel(Array.Empty<IDashboardWidget>());
        vm.Widgets.Should().BeEmpty();
    }
}
