namespace CanMonitor.Wpf.Dashboard;

public interface IDashboardWidget
{
    string Title { get; }
    int PreferredHeight { get; }
    int Row { get; }
    int Column { get; }
    int ColumnSpan { get; }
    object ViewModel { get; }
}
