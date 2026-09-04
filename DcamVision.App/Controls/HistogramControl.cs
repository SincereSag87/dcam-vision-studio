using System.Windows;
using System.Windows.Media;
using DcamVision.Imaging;

namespace DcamVision.App.Controls;

public sealed class HistogramControl : FrameworkElement
{
    public static readonly DependencyProperty BinsProperty = DependencyProperty.Register(
        nameof(Bins),
        typeof(int[]),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(Array.Empty<int>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale),
        typeof(HistogramScale),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(HistogramScale.Linear, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BlackPointProperty = DependencyProperty.Register(
        nameof(BlackPoint),
        typeof(ushort),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(ushort.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WhitePointProperty = DependencyProperty.Register(
        nameof(WhitePoint),
        typeof(ushort),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(ushort.MaxValue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThresholdEnabledProperty = DependencyProperty.Register(
        nameof(ThresholdEnabled),
        typeof(bool),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThresholdValueProperty = DependencyProperty.Register(
        nameof(ThresholdValue),
        typeof(int),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(32768, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RangeModeProperty = DependencyProperty.Register(
        nameof(RangeMode),
        typeof(HistogramDisplayRange),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(HistogramDisplayRange.Full, FrameworkPropertyMetadataOptions.AffectsRender));

    public int[] Bins
    {
        get => (int[])GetValue(BinsProperty);
        set => SetValue(BinsProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public HistogramScale Scale
    {
        get => (HistogramScale)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public ushort BlackPoint
    {
        get => (ushort)GetValue(BlackPointProperty);
        set => SetValue(BlackPointProperty, value);
    }

    public ushort WhitePoint
    {
        get => (ushort)GetValue(WhitePointProperty);
        set => SetValue(WhitePointProperty, value);
    }

    public bool ThresholdEnabled
    {
        get => (bool)GetValue(ThresholdEnabledProperty);
        set => SetValue(ThresholdEnabledProperty, value);
    }

    public int ThresholdValue
    {
        get => (int)GetValue(ThresholdValueProperty);
        set => SetValue(ThresholdValueProperty, value);
    }

    public HistogramDisplayRange RangeMode
    {
        get => (HistogramDisplayRange)GetValue(RangeModeProperty);
        set => SetValue(RangeModeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRectangle(Brushes.Transparent, null, bounds);

        if (Bins.Length == 0 || ActualWidth <= 1 || ActualHeight <= 1)
        {
            DrawEmptyState(drawingContext, bounds);
            return;
        }

        var scaled = new double[Bins.Length];
        var max = 0.0;
        for (var i = 0; i < Bins.Length; i++)
        {
            scaled[i] = HistogramScaleTransformer.Transform(Bins[i], Scale);
            max = Math.Max(max, scaled[i]);
        }

        if (max <= 0)
        {
            DrawEmptyState(drawingContext, bounds);
            return;
        }

        var baselinePen = new Pen(new SolidColorBrush(Color.FromRgb(58, 70, 84)), 1);
        drawingContext.DrawLine(baselinePen, new Point(0, ActualHeight - 0.5), new Point(ActualWidth, ActualHeight - 0.5));

        DrawDisplayRange(drawingContext, bounds);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, ActualHeight), isFilled: false, isClosed: false);

            for (var i = 0; i < Bins.Length; i++)
            {
                var x = Bins.Length == 1 ? 0 : i * (ActualWidth - 1) / (Bins.Length - 1);
                var normalized = scaled[i] / max;
                var y = ActualHeight - normalized * (ActualHeight - 4) - 2;
                context.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(Stroke, 1.4), geometry);

        DrawMarker(drawingContext, ValueToX(BlackPoint), new SolidColorBrush(Color.FromRgb(98, 177, 255)), 1.5);
        DrawMarker(drawingContext, ValueToX(WhitePoint), new SolidColorBrush(Color.FromRgb(255, 201, 112)), 1.5);

        if (ThresholdEnabled)
        {
            DrawMarker(drawingContext, ValueToX(Math.Clamp(ThresholdValue, ushort.MinValue, ushort.MaxValue)), new SolidColorBrush(Color.FromRgb(240, 104, 104)), 1.2);
        }
    }

    private static void DrawEmptyState(DrawingContext drawingContext, Rect bounds)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(58, 70, 84)), 1);
        drawingContext.DrawLine(pen, new Point(bounds.Left, bounds.Bottom - 0.5), new Point(bounds.Right, bounds.Bottom - 0.5));
    }

    private void DrawDisplayRange(DrawingContext drawingContext, Rect bounds)
    {
        var left = RangeMode == HistogramDisplayRange.Display ? ValueToX(BlackPoint) : bounds.Left;
        var right = RangeMode == HistogramDisplayRange.Display ? ValueToX(WhitePoint) : bounds.Right;
        var range = new Rect(Math.Min(left, right), bounds.Top, Math.Abs(right - left), bounds.Height);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(42, 93, 179, 199)), null, range);
    }

    private void DrawMarker(DrawingContext drawingContext, double x, Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
    }

    private double ValueToX(ushort value)
    {
        return value / (double)ushort.MaxValue * Math.Max(0, ActualWidth - 1);
    }

    private double ValueToX(int value)
    {
        return Math.Clamp(value, ushort.MinValue, ushort.MaxValue) / (double)ushort.MaxValue * Math.Max(0, ActualWidth - 1);
    }
}
