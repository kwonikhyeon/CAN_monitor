using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CanMonitor.Wpf.Transmit;

public sealed class PulseGraphControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(PulseGraphControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public static readonly DependencyProperty TraceBrushProperty = DependencyProperty.Register(
        nameof(TraceBrush),
        typeof(Brush),
        typeof(PulseGraphControl),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(61, 220, 151)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimeWindowMillisecondsProperty = DependencyProperty.Register(
        nameof(TimeWindowMilliseconds),
        typeof(double),
        typeof(PulseGraphControl),
        new FrameworkPropertyMetadata(160.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Brush TraceBrush
    {
        get => (Brush)GetValue(TraceBrushProperty);
        set => SetValue(TraceBrushProperty, value);
    }

    public double TimeWindowMilliseconds
    {
        get => (double)GetValue(TimeWindowMillisecondsProperty);
        set => SetValue(TimeWindowMillisecondsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 180 : availableSize.Height;
        return new Size(width, Math.Max(90, height));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width < 160 || bounds.Height < 90)
            return;

        var background = new SolidColorBrush(Color.FromRgb(9, 15, 26));
        var plotBackground = new SolidColorBrush(Color.FromRgb(12, 22, 38));
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 132, 148, 170)), 1);
        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(109, 125, 148)), 1.2);
        var labelBrush = new SolidColorBrush(Color.FromRgb(172, 184, 202));
        var mutedBrush = new SolidColorBrush(Color.FromRgb(112, 126, 145));

        dc.DrawRoundedRectangle(background, null, bounds, 7, 7);

        var plot = new Rect(44, 16, bounds.Width - 62, bounds.Height - 48);
        dc.DrawRoundedRectangle(plotBackground, null, plot, 5, 5);

        DrawGrid(dc, plot, gridPen, axisPen, labelBrush);

        var samples = GetSamples();
        if (samples.Count == 0)
        {
            DrawCenteredText(dc, "waiting for 0x621 / 0x622 telemetry", plot, mutedBrush, 13);
            return;
        }

        DrawWaveform(dc, plot, samples, TraceBrush);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PulseGraphControl)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnItemsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(InvalidateVisual));
            return;
        }

        InvalidateVisual();
    }

    private List<PulseTelemetrySample> GetSamples()
    {
        if (ItemsSource is null)
            return new List<PulseTelemetrySample>();

        return ItemsSource
            .OfType<PulseTelemetrySample>()
            .OrderBy(sample => sample.Timestamp)
            .ToList();
    }

    private void DrawGrid(DrawingContext dc, Rect plot, Pen gridPen, Pen axisPen, Brush labelBrush)
    {
        for (var i = 0; i <= 8; i++)
        {
            var x = plot.Left + plot.Width * i / 8.0;
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Top + plot.Height * i / 4.0;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        dc.DrawLine(axisPen, new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom));
        dc.DrawLine(axisPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));

        DrawText(dc, "1", new Point(18, plot.Top + 7), labelBrush, 12);
        DrawText(dc, "0", new Point(18, plot.Bottom - 20), labelBrush, 12);
        DrawText(dc, $"-{TimeWindowMilliseconds:0} ms", new Point(plot.Left, plot.Bottom + 8), labelBrush, 12);
        DrawText(dc, "now", new Point(plot.Right - 24, plot.Bottom + 8), labelBrush, 12);
    }

    private void DrawWaveform(DrawingContext dc, Rect plot, IReadOnlyList<PulseTelemetrySample> samples, Brush traceBrush)
    {
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromMilliseconds(Math.Max(20, TimeWindowMilliseconds));
        var start = now - window;
        var visible = samples.Where(sample => sample.Timestamp >= start && sample.Timestamp <= now).ToList();
        var previous = samples.LastOrDefault(sample => sample.Timestamp < start);
        if (previous is not null)
            visible.Insert(0, previous with { Timestamp = start });

        if (visible.Count == 0)
        {
            var latest = samples[^1];
            visible.Add(latest with { Timestamp = start });
        }

        var highY = plot.Top + plot.Height * 0.18;
        var lowY = plot.Bottom - plot.Height * 0.18;
        var tracePen = new Pen(traceBrush, 2.2)
        {
            StartLineCap = PenLineCap.Square,
            EndLineCap = PenLineCap.Square,
            LineJoin = PenLineJoin.Miter
        };

        var fillBrush = traceBrush.Clone();
        fillBrush.Opacity = 0.13;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var hasPoint = false;
            for (var i = 0; i < visible.Count; i++)
            {
                var sample = visible[i];
                var segmentStart = sample.Timestamp < start ? start : sample.Timestamp;
                var segmentEnd = i + 1 < visible.Count ? visible[i + 1].Timestamp : now;
                if (segmentEnd <= start || segmentStart >= now)
                    continue;

                if (segmentEnd > now)
                    segmentEnd = now;

                if (sample.IsValid && sample.PeriodUs > 0)
                {
                    DrawPulses(ctx, plot, start, window, segmentStart, segmentEnd, sample, highY, lowY, ref hasPoint);
                }
                else
                {
                    var y = sample.InputHigh ? highY : lowY;
                    DrawFlat(ctx, plot, start, window, segmentStart, segmentEnd, y, ref hasPoint);
                }
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(null, tracePen, geometry);

        var latestSample = visible[^1];
        DrawLatestMarker(dc, plot, latestSample, highY, lowY, traceBrush, fillBrush);
    }

    private static void DrawPulses(StreamGeometryContext ctx,
                                   Rect plot,
                                   DateTimeOffset start,
                                   TimeSpan window,
                                   DateTimeOffset segmentStart,
                                   DateTimeOffset segmentEnd,
                                   PulseTelemetrySample sample,
                                   double highY,
                                   double lowY,
                                   ref bool hasPoint)
    {
        var periodMs = Math.Max(0.05, sample.PeriodUs / 1000.0);
        var highMs = Math.Clamp(sample.HighUs / 1000.0, 0, periodMs);
        var cursor = segmentStart;

        while (cursor < segmentEnd)
        {
            var highEnd = cursor + TimeSpan.FromMilliseconds(highMs);
            var cycleEnd = cursor + TimeSpan.FromMilliseconds(periodMs);
            if (highEnd > segmentEnd)
                highEnd = segmentEnd;
            if (cycleEnd > segmentEnd)
                cycleEnd = segmentEnd;

            LineTo(ctx, MapX(plot, start, window, cursor), highY, ref hasPoint);
            LineTo(ctx, MapX(plot, start, window, highEnd), highY, ref hasPoint);
            LineTo(ctx, MapX(plot, start, window, highEnd), lowY, ref hasPoint);
            LineTo(ctx, MapX(plot, start, window, cycleEnd), lowY, ref hasPoint);

            cursor += TimeSpan.FromMilliseconds(periodMs);
        }
    }

    private static void DrawFlat(StreamGeometryContext ctx,
                                 Rect plot,
                                 DateTimeOffset start,
                                 TimeSpan window,
                                 DateTimeOffset segmentStart,
                                 DateTimeOffset segmentEnd,
                                 double y,
                                 ref bool hasPoint)
    {
        LineTo(ctx, MapX(plot, start, window, segmentStart), y, ref hasPoint);
        LineTo(ctx, MapX(plot, start, window, segmentEnd), y, ref hasPoint);
    }

    private static void LineTo(StreamGeometryContext ctx, double x, double y, ref bool hasPoint)
    {
        var point = new Point(x, y);
        if (!hasPoint)
        {
            ctx.BeginFigure(point, false, false);
            hasPoint = true;
        }
        else
        {
            ctx.LineTo(point, true, false);
        }
    }

    private static double MapX(Rect plot, DateTimeOffset start, TimeSpan window, DateTimeOffset timestamp)
    {
        var ratio = (timestamp - start).TotalMilliseconds / window.TotalMilliseconds;
        return plot.Left + Math.Clamp(ratio, 0, 1) * plot.Width;
    }

    private static void DrawLatestMarker(DrawingContext dc,
                                         Rect plot,
                                         PulseTelemetrySample sample,
                                         double highY,
                                         double lowY,
                                         Brush traceBrush,
                                         Brush fillBrush)
    {
        var y = sample.IsValid
            ? lowY - (sample.DutyPercent / 100.0) * (lowY - highY)
            : sample.InputHigh ? highY : lowY;
        var marker = new Rect(plot.Right - 8, y - 8, 16, 16);
        dc.DrawEllipse(fillBrush, null, new Point(marker.Left + 8, marker.Top + 8), 8, 8);
        dc.DrawEllipse(traceBrush, null, new Point(marker.Left + 8, marker.Top + 8), 3.5, 3.5);
    }

    private void DrawCenteredText(DrawingContext dc, string text, Rect rect, Brush brush, double fontSize)
    {
        var formatted = CreateText(text, brush, fontSize);
        dc.DrawText(formatted, new Point(
            rect.Left + (rect.Width - formatted.Width) / 2,
            rect.Top + (rect.Height - formatted.Height) / 2));
    }

    private void DrawText(DrawingContext dc, string text, Point point, Brush brush, double fontSize)
        => dc.DrawText(CreateText(text, brush, fontSize), point);

    private FormattedText CreateText(string text, Brush brush, double fontSize)
        => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
