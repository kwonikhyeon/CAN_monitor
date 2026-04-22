using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Navigation;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    public PlaceholderViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }
    public string Message => $"{Title} — 구현 예정 (Phase 3b 이후).";
}
