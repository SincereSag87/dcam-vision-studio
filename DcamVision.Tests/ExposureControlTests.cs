using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.Tests;

public sealed class ExposureControlTests
{
    [Fact]
    public void ExposureUnitConverter_ConvertsMicrosecondsToMilliseconds()
    {
        var exposure = ExposureUnitConverter.FromDisplayValue(100, ExposureUnit.Microseconds);

        Assert.Equal(0.1, exposure.TotalMilliseconds, precision: 6);
    }

    [Fact]
    public void ExposureUnitConverter_ConvertsMillisecondsToSeconds()
    {
        var exposure = ExposureUnitConverter.FromDisplayValue(1000, ExposureUnit.Milliseconds);

        Assert.Equal(1, exposure.TotalSeconds, precision: 6);
    }

    [Fact]
    public void ExposureUnitConverter_DisplaysSecondsWithoutChangingStoredExposure()
    {
        var exposure = TimeSpan.FromMilliseconds(1000);

        var displayValue = ExposureUnitConverter.ToDisplayValue(exposure, ExposureUnit.Seconds);

        Assert.Equal(1, displayValue, precision: 6);
        Assert.Equal(TimeSpan.FromSeconds(1), exposure);
    }

    [Fact]
    public void ExposureRange_ValidatesHardwareLimits()
    {
        var range = new CameraExposureRange(TimeSpan.FromMicroseconds(100), TimeSpan.FromSeconds(10));

        Assert.True(range.Contains(TimeSpan.FromMicroseconds(100)));
        Assert.False(range.Contains(TimeSpan.FromMicroseconds(99)));
        Assert.False(range.Contains(TimeSpan.FromSeconds(11)));
    }

    [Fact]
    public void ExposurePreset_DefaultsAreWithinSimulatorRange()
    {
        Assert.All(ExposurePreset.Defaults, preset => Assert.True(CameraExposureRange.Default.Contains(preset.Exposure)));
    }

    [Fact]
    public void ExposureSliderMapper_MapsRangeEndpoints()
    {
        var mapper = new ExposureSliderMapper(CameraExposureRange.Default);

        Assert.Equal(0, mapper.ToSliderValue(CameraExposureRange.Default.Minimum), precision: 6);
        Assert.Equal(100, mapper.ToSliderValue(CameraExposureRange.Default.Maximum), precision: 6);
    }

    [Fact]
    public void ExposureSliderMapper_RoundTripsMiddleExposure()
    {
        var mapper = new ExposureSliderMapper(CameraExposureRange.Default);
        var exposure = TimeSpan.FromMilliseconds(10);

        var roundTrip = mapper.FromSliderValue(mapper.ToSliderValue(exposure));

        Assert.Equal(exposure.TotalMilliseconds, roundTrip.TotalMilliseconds, precision: 3);
    }

    [Fact]
    public void ExposureSweepGenerator_GeneratesLinearSequence()
    {
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(21),
            Step = TimeSpan.FromMilliseconds(5)
        };

        var exposures = ExposureSweepGenerator.Generate(settings);

        Assert.Equal(
            [1, 6, 11, 16, 21],
            exposures.Select(exposure => (int)exposure.TotalMilliseconds).ToArray());
    }

    [Fact]
    public void ExposureSweepGenerator_RejectsStartGreaterThanEnd()
    {
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(10),
            End = TimeSpan.FromMilliseconds(1),
            Step = TimeSpan.FromMilliseconds(1)
        };

        Assert.Throws<ArgumentException>(() => ExposureSweepGenerator.Generate(settings));
    }

    [Fact]
    public void ExposureSweepGenerator_RejectsZeroStep()
    {
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(10),
            Step = TimeSpan.Zero
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => ExposureSweepGenerator.Generate(settings));
    }

    [Fact]
    public void ExposureSweepGenerator_RejectsFramesPerExposureLessThanOne()
    {
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(10),
            Step = TimeSpan.FromMilliseconds(1),
            FramesPerExposure = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => ExposureSweepGenerator.Generate(settings));
    }

    [Fact]
    public void ExposureSweepGenerator_RejectsOutOfRangeValues()
    {
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMicroseconds(50),
            End = TimeSpan.FromMilliseconds(10),
            Step = TimeSpan.FromMilliseconds(1)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => ExposureSweepGenerator.Generate(settings));
    }

    [Fact]
    public void ExposureSweepGenerator_RejectsExcessiveSweepPointCount()
    {
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(1000),
            Step = TimeSpan.FromMilliseconds(1)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => ExposureSweepGenerator.Generate(settings));
    }

    [Fact]
    public void FrameStatisticsCalculator_ComputesSaturationMetrics()
    {
        var frame = CreateFrame([10, 65_499, 65_500, 65_535]);

        var statistics = FrameStatisticsCalculator.Calculate(frame);

        Assert.Equal(2, statistics.SaturatedPixelCount);
        Assert.Equal(50, statistics.SaturationPercentage);
    }

    [Fact]
    public async Task ExposureSweepRunner_CapturesFramesAcrossExposureSequence()
    {
        var service = await CreateConnectedServiceAsync();
        var runner = new ExposureSweepRunner(service);
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(11),
            Step = TimeSpan.FromMilliseconds(5)
        };

        var result = await runner.RunAsync(settings);

        Assert.Equal(3, result.Frames.Count);
        Assert.Equal([1, 6, 11], result.Frames.Select(frame => (int)frame.Exposure.TotalMilliseconds).ToArray());
        Assert.All(result.Frames, frame => Assert.True(frame.Statistics.Mean > 0));
    }

    [Fact]
    public async Task ExposureSweepRunner_CapturesMultipleFramesPerExposure()
    {
        var service = await CreateConnectedServiceAsync();
        var runner = new ExposureSweepRunner(service);
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(6),
            Step = TimeSpan.FromMilliseconds(5),
            FramesPerExposure = 2
        };

        var result = await runner.RunAsync(settings);

        Assert.Equal(4, result.Frames.Count);
        Assert.Equal(2, result.Frames.Count(frame => frame.Exposure == TimeSpan.FromMilliseconds(1)));
        Assert.Equal(2, result.Frames.Count(frame => frame.Exposure == TimeSpan.FromMilliseconds(6)));
    }

    [Fact]
    public async Task ExposureSweepRunner_ReportsProgress()
    {
        var service = await CreateConnectedServiceAsync();
        var runner = new ExposureSweepRunner(service);
        var reports = new List<ExposureSweepProgress>();
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(6),
            Step = TimeSpan.FromMilliseconds(5)
        };

        await runner.RunAsync(settings, new InlineProgress<ExposureSweepProgress>(reports.Add));

        Assert.Equal(2, reports.Count);
        Assert.Equal(100, reports.Last().CompletionPercentage);
    }

    [Fact]
    public async Task ExposureSweepRunner_HonorsCancellation()
    {
        var service = await CreateConnectedServiceAsync();
        var runner = new ExposureSweepRunner(service);
        using var cancellation = new CancellationTokenSource();
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(100),
            Step = TimeSpan.FromMilliseconds(1),
            DelayBetweenCaptures = TimeSpan.FromMilliseconds(10)
        };
        var progress = new InlineProgress<ExposureSweepProgress>(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(settings, progress, cancellation.Token));
    }

    [Fact]
    public async Task ExposureSweepRunner_RestoresPreviousExposureAfterCompletion()
    {
        var service = await CreateConnectedServiceAsync();
        await service.SetExposureAsync(TimeSpan.FromMilliseconds(25));
        var runner = new ExposureSweepRunner(service);
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(6),
            Step = TimeSpan.FromMilliseconds(5)
        };

        await runner.RunAsync(settings);

        Assert.Equal(TimeSpan.FromMilliseconds(25), service.CurrentSettings.Exposure);
    }

    [Fact]
    public async Task ExposureSweepRunner_RestoresPreviousExposureAfterCancellation()
    {
        var service = await CreateConnectedServiceAsync();
        await service.SetExposureAsync(TimeSpan.FromMilliseconds(25));
        var runner = new ExposureSweepRunner(service);
        using var cancellation = new CancellationTokenSource();
        var settings = new ExposureSweepSettings
        {
            Start = TimeSpan.FromMilliseconds(1),
            End = TimeSpan.FromMilliseconds(100),
            Step = TimeSpan.FromMilliseconds(1),
            DelayBetweenCaptures = TimeSpan.FromMilliseconds(10)
        };
        var progress = new InlineProgress<ExposureSweepProgress>(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(settings, progress, cancellation.Token));

        Assert.Equal(TimeSpan.FromMilliseconds(25), service.CurrentSettings.Exposure);
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

    private static async Task<SimulatedCameraService> CreateConnectedServiceAsync()
    {
        var service = new SimulatedCameraService();
        var camera = Assert.Single(await service.DiscoverAsync());
        await service.ConnectAsync(camera.Id);
        return service;
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }
}
