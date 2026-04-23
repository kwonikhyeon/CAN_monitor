namespace CanMonitor.Wpf.Dashboard.Widgets;

public sealed class PlaceholderWidget : IDashboardWidget
{
    public PlaceholderWidget(string title, int preferredHeight, int row = 0, int column = 0, int columnSpan = 1)
    {
        Title = title;
        PreferredHeight = preferredHeight;
        Row = row;
        Column = column;
        ColumnSpan = columnSpan;
        ViewModel = new PlaceholderWidgetViewModel(title);
    }

    public string Title { get; }
    public int PreferredHeight { get; }
    public int Row { get; }
    public int Column { get; }
    public int ColumnSpan { get; }
    public object ViewModel { get; }
}
