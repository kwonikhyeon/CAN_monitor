using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Dashboard;

public sealed partial class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(IEnumerable<IDashboardWidget> widgets)
    {
        Widgets = widgets.ToList();
    }

    public IReadOnlyList<IDashboardWidget> Widgets { get; }
}
