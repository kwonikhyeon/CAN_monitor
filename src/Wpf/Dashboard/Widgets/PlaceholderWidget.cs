namespace CanMonitor.Wpf.Dashboard.Widgets;

public sealed class PlaceholderWidget : IDashboardWidget
{
    public PlaceholderWidget(string title, int preferredWidth, int preferredHeight)
    {
        Title = title;
        PreferredWidth = preferredWidth;
        PreferredHeight = preferredHeight;
        ViewModel = new PlaceholderWidgetViewModel(title);
    }

    public string Title { get; }
    public int PreferredWidth { get; }
    public int PreferredHeight { get; }
    public object ViewModel { get; }
}
