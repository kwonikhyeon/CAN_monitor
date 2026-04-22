using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Dashboard.Widgets;

public sealed partial class PlaceholderWidgetViewModel : ObservableObject
{
    public PlaceholderWidgetViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }
    public string Message => $"{Title} — Phase 3b 구현 예정";
}
