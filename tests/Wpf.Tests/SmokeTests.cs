using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests;

public class SmokeTests
{
    [Fact]
    public void Assembly_is_loaded()
    {
        typeof(App).Assembly.FullName.Should().Contain("CanMonitor.Wpf");
    }
}
