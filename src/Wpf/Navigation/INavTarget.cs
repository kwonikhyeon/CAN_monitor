namespace CanMonitor.Wpf.Navigation;

public interface INavTarget
{
    string Key { get; }
    string Title { get; }
    string IconGlyph { get; }
    int Order { get; }
    object CreateViewModel(IServiceProvider sp);
}
