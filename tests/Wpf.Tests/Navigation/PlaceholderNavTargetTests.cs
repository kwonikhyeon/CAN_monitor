using CanMonitor.Wpf.Navigation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanMonitor.Wpf.Tests.Navigation;

public class PlaceholderNavTargetTests
{
    [Fact]
    public void CreateViewModel_returns_Placeholder_with_title()
    {
        var target = new PlaceholderNavTarget("Raw", "BulletedList", "Raw Log", 20);
        var sp = new ServiceCollection().BuildServiceProvider();
        var vm = target.CreateViewModel(sp);
        vm.Should().BeOfType<PlaceholderViewModel>()
          .Which.Title.Should().Be("Raw Log");
    }

    [Fact]
    public void Properties_reflect_constructor_arguments()
    {
        var target = new PlaceholderNavTarget("Transmit", "\uE724", "Transmit", 30);
        target.Key.Should().Be("Transmit");
        target.Title.Should().Be("Transmit");
        target.IconGlyph.Should().Be("\uE724");
        target.Order.Should().Be(30);
    }
}
