using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Navigation;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class ShellViewModelTests
{
    private static IServiceProvider BuildSp()
    {
        var svc = new ServiceCollection();
        svc.AddSingleton(new DashboardViewModel(Array.Empty<IDashboardWidget>()));
        return svc.BuildServiceProvider();
    }

    [Fact]
    public void NavTargets_sorted_by_Order_ascending()
    {
        var targets = new INavTarget[]
        {
            new PlaceholderNavTarget("Z", "icon", "Last", 70),
            new DashboardNavTarget(),
            new PlaceholderNavTarget("M", "icon", "Mid", 30)
        };

        var vm = new ShellViewModel(targets, BuildSp(),
            session: null!, status: null!);

        vm.NavTargets.Select(t => t.Order).Should().ContainInOrder(10, 30, 70);
    }

    [Fact]
    public void Default_selected_is_Dashboard()
    {
        var vm = new ShellViewModel(
            new INavTarget[] { new DashboardNavTarget(), new PlaceholderNavTarget("Raw", "icon", "Raw", 20) },
            BuildSp(), session: null!, status: null!);

        vm.SelectedTarget.Key.Should().Be("Dashboard");
        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void Changing_SelectedTarget_updates_CurrentViewModel()
    {
        var vm = new ShellViewModel(
            new INavTarget[] { new DashboardNavTarget(), new PlaceholderNavTarget("Raw", "icon", "Raw", 20) },
            BuildSp(), session: null!, status: null!);

        vm.SelectedTarget = vm.NavTargets.First(t => t.Key == "Raw");

        vm.CurrentViewModel.Should().BeOfType<PlaceholderViewModel>();
    }
}
