using CanMonitor.Wpf.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace CanMonitor.Wpf.Transmit;

public sealed class TransmitNavTarget : INavTarget
{
    public string Key => "Transmit";
    public string Title => "Transmit";
    public string IconGlyph => "\uE724";
    public int Order => 30;

    public object CreateViewModel(IServiceProvider sp) => sp.GetRequiredService<TransmitViewModel>();
}
