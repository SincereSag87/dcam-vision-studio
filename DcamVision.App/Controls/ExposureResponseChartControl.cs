using System.Windows;
using System.Windows.Media;
using DcamVision.App.ViewModels;

namespace DcamVision.App.Controls;

public sealed class ExposureResponseChartControl : FrameworkElement
{
    public static readonly DependencyProperty ResultsProperty = DependencyProperty.Register(
        nameof(Results),
        typeof(IEnumerable<SweepResultFrameViewModel>),
        typeof(ExposureResponseChartControl),
        new FrameworkPropertyMetadata(Array.Empty<SweepResultFrameViewModel>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<SweepResultFrameViewModel> Results
    {
        get => (IEnumerable<SweepResultFrameViewModel>)GetValue(ResultsProperty);
        set => SetValue(ResultsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRectangle(Brushes.Transparent, null, bounds);
        DrawAxes(drawingContext, bounds);

        var results = Results.ToArray();
        if (results.Length == 0 || ActualWidth <= 4 || ActualHeight <= 4)
        {
            return;
        }

        var minimumExposure = results.Min(item => item.Result.Exposure.TotalMicroseconds);
        var maximumExposure = results.Max(item => item.Result.Exposure.TotalMicroseconds);
        var maximumMean = Math.Max(1.0, results.Max(item => item.Result.Statistics.Mean));
        var left = 28.0;
        var top = 8.0;
        var right = ActualWidth - 8.0;
        var bottom = ActualHeight - 22.0;
        var width = Math.Max(1.0, right - left);
        var height = Math.Max(1.0, bottom - top);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i < results.Length; i++)
            {
                var item = results[i];
                var exposure = item.Result.Exposure.TotalMicroseconds;
                var x = maximumExposure.Equals(minimumExposure)
                    ? left + width / 2
                    : left + (exposure - minimumExposure) / (maximumExposure - minimumExposure) * width;
                var y = bottom - item.Result.Statistics.Mean / maximumMean * height;
                var point = new Point(x, y);

                if (i == 0)
                {
                    context.BeginFigure(point, isFilled: false, isClosed: false);
                }
                else
                {
                    context.LineTo(point, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        var lineBrush = new SolidColorBrush(Color.FromRgb(93, 179, 199));
        drawingContext.DrawGeometry(null, new Pen(lineBrush, 1.6), geometry);

        foreach (var item in results)
        {
            var exposure = item.Result.Exposure.TotalMicroseconds;
            var x = maximumExposure.Equals(minimumExposure)
                ? left + width / 2
                : left + (exposure - minimumExposure) / (maximumExposure - minimumExposure) * width;
            var y = bottom - item.Result.Statistics.Mean / maximumMean * height;
            var brush = item.Result.Statistics.SaturationPercentage >= 10
                ? new SolidColorBrush(Color.FromRgb(216, 108, 108))
                : lineBrush;
            drawingContext.DrawEllipse(brush, null, new Point(x, y), 2.8, 2.8);
        }
    }

    private static void DrawAxes(DrawingContext drawingContext, Rect bounds)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(58, 70, 84)), 1);
        var left = 28.0;
        var bottom = bounds.Height - 22.0;
        drawingContext.DrawLine(pen, new Point(left, 8), new Point(left, bottom));
        drawingContext.DrawLine(pen, new Point(left, bottom), new Point(bounds.Width - 8, bottom));
    }
}
