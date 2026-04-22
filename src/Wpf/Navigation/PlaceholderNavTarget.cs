namespace CanMonitor.Wpf.Navigation;

public sealed class PlaceholderNavTarget : INavTarget
{
    public PlaceholderNavTarget(string key, string iconGlyph, string title, int order)
    {
        Key = key;
        IconGlyph = iconGlyph;
        Title = title;
        Order = order;
    }

    public string Key { get; }
    public string Title { get; }
    public string IconGlyph { get; }
    public int Order { get; }

    public object CreateViewModel(IServiceProvider sp) => new PlaceholderViewModel(Title);
}
