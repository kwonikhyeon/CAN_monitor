using CanMonitor.Wpf.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    public ShellViewModel(
        IEnumerable<INavTarget> targets,
        IServiceProvider sp,
        SessionViewModel session,
        StatusBarViewModel status)
    {
        _sp = sp;
        NavTargets = targets.OrderBy(t => t.Order).ToList();
        Session = session;
        Status = status;
        SelectedTarget = NavTargets.FirstOrDefault(t => t.Key == "Dashboard") ?? NavTargets[0];
    }

    public IReadOnlyList<INavTarget> NavTargets { get; }
    public SessionViewModel Session { get; }
    public StatusBarViewModel Status { get; }

    private INavTarget _selectedTarget = null!;
    public INavTarget SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
                CurrentViewModel = value.CreateViewModel(_sp);
        }
    }

    [ObservableProperty] private object _currentViewModel = new();
}
