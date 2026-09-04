using System.Windows;
using System.Windows.Media;

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

        var max = Bins.Max();
        if (max <= 0)
        {
            DrawEmptyState(drawingContext, bounds);
            return;
        }

        var baselinePen = new Pen(new SolidColorBrush(Color.FromRgb(58, 70, 84)), 1);
        drawingContext.DrawLine(baselinePen, new Point(0, ActualHeight - 0.5), new Point(ActualWidth, ActualHeight - 0.5));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, ActualHeight), isFilled: false, isClosed: false);

            for (var i = 0; i < Bins.Length; i++)
            {
                var x = Bins.Length == 1 ? 0 : i * (ActualWidth - 1) / (Bins.Length - 1);
                var normalized = Bins[i] / (double)max;
                var y = ActualHeight - normalized * (ActualHeight - 4) - 2;
                context.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(Stroke, 1.4), geometry);
    }

    private static void DrawEmptyState(DrawingContext drawingContext, Rect bounds)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(58, 70, 84)), 1);
        drawingContext.DrawLine(pen, new Point(bounds.Left, bounds.Bottom - 0.5), new Point(bounds.Right, bounds.Bottom - 0.5));
    }
}
