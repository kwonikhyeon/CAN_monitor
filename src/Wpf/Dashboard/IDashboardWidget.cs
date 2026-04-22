namespace CanMonitor.Wpf.Dashboard;

public interface IDashboardWidget
{
    string Title { get; }
    int PreferredWidth { get; }
    int PreferredHeight { get; }
    object ViewModel { get; }
}
