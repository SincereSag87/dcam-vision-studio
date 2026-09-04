using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.Tests;

public sealed class ImageDisplayProcessingTests
{
    [Fact]
    public void ImageDisplayProcessor_DoesNotMutateRawPixels()
    {
        var frame = CreateFrame([0, 1000, 32000, 65535]);
        var original = frame.Pixels.ToArray();
        var processor = new ImageDisplayProcessor();

        _ = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 1000,
            WhitePoint = 64000,
            Gamma = 1.8,
            Invert = true
        });

        Assert.Equal(original, frame.Pixels);
    }

    [Fact]
    public void ImageDisplayProcessor_MapsManualBlackWhiteLinearly()
    {
        var frame = CreateFrame([100, 200, 300]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 100,
            WhitePoint = 300
        });

        Assert.Equal([0, 128, 255], display.Pixels);
        Assert.Equal(100, display.EffectiveBlackPoint);
        Assert.Equal(300, display.EffectiveWhitePoint);
    }

    [Fact]
    public void ImageDisplaySettings_RejectsInvalidBlackWhiteRange()
    {
        var settings = ImageDisplaySettings.Default with
        {
            BlackPoint = 200,
            WhitePoint = 200
        };

        Assert.Throws<ArgumentException>(settings.Validate);
    }

    [Fact]
    public void ImageDisplayProcessor_GammaOneKeepsLinearMapping()
    {
        var frame = CreateFrame([0, 25, 50, 100]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 0,
            WhitePoint = 100,
            Gamma = 1.0
        });

        Assert.Equal([0, 64, 128, 255], display.Pixels);
    }

    [Fact]
    public void ImageDisplayProcessor_GammaBelowOneDarkensMidtones()
    {
        var frame = CreateFrame([25]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 0,
            WhitePoint = 100,
            Gamma = 0.5
        });

        Assert.Equal(16, display.Pixels[0]);
    }

    [Fact]
    public void ImageDisplayProcessor_GammaAboveOneBrightensMidtones()
    {
        var frame = CreateFrame([25]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 0,
            WhitePoint = 100,
            Gamma = 2.0
        });

        Assert.Equal(128, display.Pixels[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.09)]
    [InlineData(5.1)]
    public void ImageDisplaySettings_RejectsInvalidGamma(double gamma)
    {
        var settings = ImageDisplaySettings.Default with { Gamma = gamma };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void ImageDisplayProcessor_InvertsPreviewOnly()
    {
        var frame = CreateFrame([0, 100]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 0,
            WhitePoint = 100,
            Invert = true
        });

        Assert.Equal([255, 0], display.Pixels);
        Assert.Equal([0, 100], frame.Pixels);
    }

    [Fact]
    public void ImageDisplayProcessor_ThresholdUsesGreaterThanOrEqualAsWhite()
    {
        var frame = CreateFrame([99, 100, 101]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            ThresholdEnabled = true,
            ThresholdValue = 100
        });

        Assert.Equal([0, 255, 255], display.Pixels);
        Assert.Equal(1, display.Clipping.ThresholdBlackPixels);
        Assert.Equal(2, display.Clipping.ThresholdWhitePixels);
    }

    [Fact]
    public void ImageDisplayProcessor_MinMaxAutoContrastUsesFrameRange()
    {
        var frame = CreateFrame([1000, 2000, 3000]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            AutoContrastMode = AutoContrastMode.MinMax
        });

        Assert.Equal(1000, display.EffectiveBlackPoint);
        Assert.Equal(3000, display.EffectiveWhitePoint);
        Assert.Equal([0, 128, 255], display.Pixels);
    }

    [Fact]
    public void ImageDisplayProcessor_PercentileAutoContrastIgnoresSingleHighOutlier()
    {
        ushort[] pixels = [0, .. Enumerable.Repeat((ushort)1000, 98), ushort.MaxValue];
        var frame = CreateFrame(pixels);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            AutoContrastMode = AutoContrastMode.Percentile,
            LowerPercentile = 1,
            UpperPercentile = 99
        });

        Assert.Equal(0, display.EffectiveBlackPoint);
        Assert.Equal(1000, display.EffectiveWhitePoint);
    }

    [Fact]
    public void HistogramPercentileCalculator_ReturnsExpectedKnownDistribution()
    {
        var frame = CreateFrame([0, 10, 10, 20, 20, 20, 30, 30, 40, 50]);

        Assert.Equal(0, HistogramPercentileCalculator.Calculate(frame, 10));
        Assert.Equal(20, HistogramPercentileCalculator.Calculate(frame, 50));
        Assert.Equal(50, HistogramPercentileCalculator.Calculate(frame, 100));
    }

    [Fact]
    public void HistogramAnalyzer_ComputesMedianAndStandardDeviation()
    {
        var frame = CreateFrame([0, 10, 20, 30]);

        var result = HistogramAnalyzer.Analyze(frame);

        Assert.Equal(15, result.Median);
        Assert.Equal(Math.Sqrt(125), result.StandardDeviation, precision: 3);
        Assert.Equal(4, result.PixelCount);
    }

    [Fact]
    public void HistogramAnalyzer_ComputesSaturationMetrics()
    {
        var frame = CreateFrame([0, 65500, ushort.MaxValue, 100]);

        var result = HistogramAnalyzer.Analyze(frame);

        Assert.Equal(2, result.SaturatedPixelCount);
        Assert.Equal(50, result.SaturationPercentage);
    }

    [Fact]
    public void ImageDisplayProcessor_ComputesDisplayClippingCountsAndPercentages()
    {
        var frame = CreateFrame([0, 10, 20, 30, 40]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default with
        {
            BlackPoint = 10,
            WhitePoint = 30
        });

        Assert.Equal(2, display.Clipping.BlackClippedPixels);
        Assert.Equal(2, display.Clipping.WhiteClippedPixels);
        Assert.Equal(40, display.Clipping.BlackClippedPercentage);
        Assert.Equal(40, display.Clipping.WhiteClippedPercentage);
    }

    [Fact]
    public void HistogramScaleTransformer_LogarithmicCompressesLargeBins()
    {
        var linearSmall = HistogramScaleTransformer.Transform(10, HistogramScale.Linear);
        var linearLarge = HistogramScaleTransformer.Transform(1000, HistogramScale.Linear);
        var logSmall = HistogramScaleTransformer.Transform(10, HistogramScale.Logarithmic);
        var logLarge = HistogramScaleTransformer.Transform(1000, HistogramScale.Logarithmic);

        Assert.True(linearLarge / linearSmall > logLarge / logSmall);
    }

    [Fact]
    public void ImageDisplaySettings_DefaultIsRawDisplayMapping()
    {
        var settings = ImageDisplaySettings.Default;

        Assert.Equal(0, settings.BlackPoint);
        Assert.Equal(ushort.MaxValue, settings.WhitePoint);
        Assert.Equal(1.0, settings.Gamma);
        Assert.False(settings.Invert);
        Assert.False(settings.ThresholdEnabled);
        Assert.Equal(AutoContrastMode.Off, settings.AutoContrastMode);
        Assert.Equal(HistogramDisplayRange.Full, settings.HistogramDisplayRange);
    }

    [Fact]
    public void ImageDisplaySettings_DisplayPresetsAreDeterministic()
    {
        var settings = ImageDisplaySettings.Default;

        Assert.Equal(AutoContrastMode.MinMax, settings.ApplyPreset(DisplayPreset.AutoMinMax).AutoContrastMode);
        Assert.Equal(AutoContrastMode.Percentile, settings.ApplyPreset(DisplayPreset.AutoPercentile).AutoContrastMode);
        Assert.Equal(5.0, settings.ApplyPreset(DisplayPreset.HighContrast).LowerPercentile);
        Assert.Equal(0.8, settings.ApplyPreset(DisplayPreset.LowContrast).Gamma);
    }

    [Fact]
    public void ImageDisplayProcessor_HandlesExtremePixelValues()
    {
        var frame = CreateFrame([ushort.MinValue, ushort.MaxValue]);
        var processor = new ImageDisplayProcessor();

        var display = processor.Process(frame, ImageDisplaySettings.Default);

        Assert.Equal([0, 255], display.Pixels);
    }

    private static CameraFrame CreateFrame(ushort[] pixels)
    {
        return new CameraFrame
        {
            Width = pixels.Length,
            Height = 1,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = pixels,
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 1,
            Exposure = TimeSpan.FromMilliseconds(25)
        };
    }
}
