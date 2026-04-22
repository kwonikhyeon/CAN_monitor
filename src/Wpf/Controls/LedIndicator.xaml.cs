using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanMonitor.Wpf.Infrastructure;

namespace CanMonitor.Wpf.Controls;

public partial class LedIndicator : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(ConnectionState), typeof(LedIndicator),
        new PropertyMetadata(ConnectionState.Disconnected, OnStateChanged));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(LedIndicator),
        new PropertyMetadata(null));

    public LedIndicator()
    {
        InitializeComponent();
        UpdateBrush();
    }

    public ConnectionState State
    {
        get => (ConnectionState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        private set => SetValue(BrushProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LedIndicator)d).UpdateBrush();

    private void UpdateBrush()
    {
        var resourceKey = State switch
        {
            ConnectionState.Connected => "LedConnectedBrush",
            ConnectionState.Error => "LedErrorBrush",
            ConnectionState.Connecting => "WarningBrush",
            _ => "LedDisconnectedBrush"
        };
        Brush = TryFindResource(resourceKey) as Brush;
    }
}
