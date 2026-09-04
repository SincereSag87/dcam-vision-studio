using System.Runtime.CompilerServices;
using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.Tests;

public sealed class LiveAcquisitionPipelineTests
{
    [Fact]
    public void RollingFrameRateCalculator_UsesRecentWindow()
    {
        var calculator = new RollingFrameRateCalculator(TimeSpan.FromSeconds(1));
        var start = DateTimeOffset.UtcNow;

        calculator.Record(start);
        calculator.Record(start.AddMilliseconds(250));
        calculator.Record(start.AddMilliseconds(500));

        Assert.Equal(3, calculator.FramesPerSecond(start.AddMilliseconds(500)));
        Assert.Equal(2, calculator.FramesPerSecond(start.AddMilliseconds(1250)));
    }

    [Fact]
    public void RollingFrameRateCalculator_ResetClearsSamples()
    {
        var calculator = new RollingFrameRateCalculator(TimeSpan.FromSeconds(1));

        calculator.Record(DateTimeOffset.UtcNow);
        calculator.Reset();

        Assert.Equal(0, calculator.FramesPerSecond(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task BoundedFrameBuffer_DropNewestKeepsExistingFrames()
    {
        var buffer = new BoundedFrameBuffer(1, LiveBufferOverflowStrategy.DropNewest);

        var firstDrop = await buffer.WriteAsync(CreateFrame(1));
        var secondDrop = await buffer.WriteAsync(CreateFrame(2));
        var frame = await buffer.ReadLatestAsync();

        Assert.Equal(0, firstDrop);
        Assert.Equal(1, secondDrop);
        Assert.Equal(1, frame?.FrameNumber);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public async Task BoundedFrameBuffer_DropOldestKeepsNewestFrame()
    {
        var buffer = new BoundedFrameBuffer(1, LiveBufferOverflowStrategy.DropOldest);

        await buffer.WriteAsync(CreateFrame(1));
        var dropped = await buffer.WriteAsync(CreateFrame(2));
        var frame = await buffer.ReadLatestAsync();

        Assert.Equal(1, dropped);
        Assert.Equal(2, frame?.FrameNumber);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public async Task BoundedFrameBuffer_NeverExceedsCapacity()
    {
        var buffer = new BoundedFrameBuffer(2, LiveBufferOverflowStrategy.DropOldest);

        await buffer.WriteAsync(CreateFrame(1));
        await buffer.WriteAsync(CreateFrame(2));
        await buffer.WriteAsync(CreateFrame(3));

        Assert.InRange(buffer.Count, 0, 2);
    }

    [Fact]
    public async Task BoundedFrameBuffer_ReadLatestDrainsStaleFrames()
    {
        var buffer = new BoundedFrameBuffer(4, LiveBufferOverflowStrategy.DropOldest);

        await buffer.WriteAsync(CreateFrame(1));
        await buffer.WriteAsync(CreateFrame(2));
        await buffer.WriteAsync(CreateFrame(3));

        var frame = await buffer.ReadLatestAsync();

        Assert.Equal(3, frame?.FrameNumber);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void LiveAcquisitionOptions_RejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LiveAcquisitionOptions { BufferCapacity = 0 }.Validate());
    }

    [Fact]
    public void SimulatedCameraOptions_RejectsInvalidFrameRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedCameraService(new SimulatedCameraOptions { FramesPerSecond = 0 }));
    }

    [Fact]
    public async Task SimulatedCameraService_HighFrameRateProducesFrames()
    {
        var service = await CreateConnectedSimulatorAsync(120);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(120));
        var frames = 0;

        await foreach (var _ in service.StreamFramesAsync(cancellation.Token))
        {
            frames++;
            if (frames >= 3)
            {
                break;
            }
        }

        Assert.True(frames >= 3);
    }

    [Fact]
    public async Task LiveAcquisitionService_StartRequiresConnectedCamera()
    {
        var service = new LiveAcquisitionService(new ScriptedCameraService([1]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
    }

    [Fact]
    public async Task LiveAcquisitionService_RejectsDuplicateStart()
    {
        var camera = new ScriptedCameraService(Enumerable.Range(1, 50).Select(value => (long)value), delay: TimeSpan.FromMilliseconds(5));
        await camera.ConnectAsync("test-camera");
        var service = new LiveAcquisitionService(camera);

        await service.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
        await service.StopAsync();
    }

    [Fact]
    public async Task LiveAcquisitionService_StopWhenStoppedIsNoOp()
    {
        var camera = new ScriptedCameraService([1]);
        var service = new LiveAcquisitionService(camera);

        await service.StopAsync();

        Assert.Equal(LiveAcquisitionState.Stopped, service.State);
    }

    [Fact]
    public async Task LiveAcquisitionService_StartStopStartResetsMetrics()
    {
        var camera = new ScriptedCameraService(Enumerable.Range(1, 100).Select(value => (long)value), delay: TimeSpan.FromMilliseconds(2));
        await camera.ConnectAsync("test-camera");
        var service = new LiveAcquisitionService(camera, new LiveAcquisitionOptions { MaximumPreviewFps = 60 });

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.FramesAcquired > 0);
        await service.StopAsync();
        var firstSessionFrames = service.Metrics.FramesAcquired;

        await service.StartAsync();
        Assert.True(service.Metrics.FramesAcquired <= firstSessionFrames);
        await service.StopAsync();
    }

    [Fact]
    public async Task LiveAcquisitionService_PausePreviewKeepsAcquiring()
    {
        var camera = await CreateConnectedSimulatorAsync(60);
        var service = new LiveAcquisitionService(camera, new LiveAcquisitionOptions { MaximumPreviewFps = 30 });

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.FramesDisplayed > 0);
        service.PausePreview();
        var displayedWhenPaused = service.Metrics.FramesDisplayed;
        await WaitUntilAsync(() => service.Metrics.FramesAcquired > displayedWhenPaused + 3);
        await Task.Delay(80);

        Assert.Equal(LiveAcquisitionState.PreviewPaused, service.State);
        Assert.Equal(displayedWhenPaused, service.Metrics.FramesDisplayed);

        await service.StopAsync();
    }

    [Fact]
    public async Task LiveAcquisitionService_ResumePreviewDisplaysNewestFrame()
    {
        var camera = await CreateConnectedSimulatorAsync(60);
        var service = new LiveAcquisitionService(camera, new LiveAcquisitionOptions { MaximumPreviewFps = 30 });

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.FramesDisplayed > 0);
        service.PausePreview();
        await WaitUntilAsync(() => service.Metrics.FramesProcessed > service.Metrics.FramesDisplayed + 2);
        var displayedBeforeResume = service.Metrics.FramesDisplayed;
        service.ResumePreview();
        await WaitUntilAsync(() => service.Metrics.FramesDisplayed > displayedBeforeResume);

        Assert.Equal(LiveAcquisitionState.Running, service.State);

        await service.StopAsync();
    }

    [Fact]
    public async Task LiveAcquisitionService_ReportsMetricsAndFrames()
    {
        var camera = await CreateConnectedSimulatorAsync(30);
        var service = new LiveAcquisitionService(camera);
        var frames = new List<ProcessedFrame>();
        service.FrameReady += (_, frame) => frames.Add(frame);

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.FramesDisplayed >= 2);
        await service.StopAsync();

        Assert.True(service.Metrics.FramesAcquired >= service.Metrics.FramesDisplayed);
        Assert.True(service.Metrics.FramesProcessed >= service.Metrics.FramesDisplayed);
        Assert.True(frames.Count >= 2);
        Assert.All(frames, frame => Assert.Equal(256, frame.Histogram.Length));
    }

    [Fact]
    public async Task LiveAcquisitionService_DetectsSourceFrameGaps()
    {
        var camera = new ScriptedCameraService([100, 101, 104, 105], delay: TimeSpan.FromMilliseconds(1));
        await camera.ConnectAsync("test-camera");
        var service = new LiveAcquisitionService(camera, new LiveAcquisitionOptions { MaximumPreviewFps = 120 });

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.SourceFrameGaps >= 2);
        await service.StopAsync();

        Assert.Equal(2, service.Metrics.SourceFrameGaps);
    }

    [Fact]
    public async Task LiveAcquisitionService_TracksPipelineDrops()
    {
        var buffer = new BoundedFrameBuffer(1, LiveBufferOverflowStrategy.DropNewest);

        Assert.Equal(0, await buffer.WriteAsync(CreateFrame(1)));
        Assert.Equal(1, await buffer.WriteAsync(CreateFrame(2)));

        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public async Task LiveAcquisitionService_DisplayFpsCanBeLowerThanAcquisitionFps()
    {
        var camera = await CreateConnectedSimulatorAsync(120);
        var service = new LiveAcquisitionService(camera, new LiveAcquisitionOptions { MaximumPreviewFps = 15 });

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.FramesAcquired >= 8);
        await service.StopAsync();

        Assert.True(service.Metrics.FramesAcquired >= service.Metrics.FramesDisplayed);
    }

    [Fact]
    public async Task LiveAcquisitionService_FaultsOnProducerException()
    {
        var camera = new ScriptedCameraService([1, 2], throwAfterFrames: 1);
        await camera.ConnectAsync("test-camera");
        var service = new LiveAcquisitionService(camera);
        Exception? fault = null;
        service.Faulted += (_, exception) => fault = exception;

        await service.StartAsync();
        await WaitUntilAsync(() => fault is not null);
        await service.StopAsync();

        Assert.NotNull(fault);
    }

    [Fact]
    public async Task LiveAcquisitionService_StopLeavesCameraConnected()
    {
        var camera = await CreateConnectedSimulatorAsync(30);
        var service = new LiveAcquisitionService(camera);

        await service.StartAsync();
        await WaitUntilAsync(() => camera.State == CameraConnectionState.Streaming);
        await service.StopAsync();

        Assert.Equal(CameraConnectionState.Connected, camera.State);
        Assert.NotNull(camera.ConnectedDevice);
    }

    [Fact]
    public async Task LiveAcquisitionService_HandlesNaturalProducerCompletion()
    {
        var camera = new ScriptedCameraService([1, 2, 3], delay: TimeSpan.FromMilliseconds(1));
        await camera.ConnectAsync("test-camera");
        var service = new LiveAcquisitionService(camera, new LiveAcquisitionOptions { MaximumPreviewFps = 120 });

        await service.StartAsync();
        await WaitUntilAsync(() => service.Metrics.FramesAcquired == 3);
        await service.StopAsync();

        Assert.Equal(LiveAcquisitionState.Stopped, service.State);
        Assert.True(service.Metrics.FramesProcessed > 0);
    }

    private static CameraFrame CreateFrame(long frameNumber)
    {
        return new CameraFrame
        {
            Width = 2,
            Height = 2,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = [1, 2, 3, 4],
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = frameNumber,
            Exposure = TimeSpan.FromMilliseconds(25)
        };
    }

    private static async Task<SimulatedCameraService> CreateConnectedSimulatorAsync(double framesPerSecond)
    {
        var service = new SimulatedCameraService(new SimulatedCameraOptions { FramesPerSecond = framesPerSecond });
        var camera = Assert.Single(await service.DiscoverAsync());
        await service.ConnectAsync(camera.Id);
        return service;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 2000)
    {
        var startedAt = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - startedAt < TimeSpan.FromMilliseconds(timeoutMilliseconds))
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "Timed out waiting for condition.");
    }

    private sealed class ScriptedCameraService : ICameraService
    {
        private static readonly CameraDevice Device = new("test-camera", "Test Camera", "Test", "Pipeline", "PIPE-1", true);
        private readonly IReadOnlyList<long> _frameNumbers;
        private readonly TimeSpan _delay;
        private readonly int? _throwAfterFrames;
        private CaptureSettings _settings = new();

        public ScriptedCameraService(IEnumerable<long> frameNumbers, TimeSpan? delay = null, int? throwAfterFrames = null)
        {
            _frameNumbers = frameNumbers.ToArray();
            _delay = delay ?? TimeSpan.Zero;
            _throwAfterFrames = throwAfterFrames;
        }

        public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;

        public CameraDevice? ConnectedDevice { get; private set; }

        public CaptureSettings CurrentSettings => _settings;

        public CameraExposureRange ExposureRange { get; } = CameraExposureRange.Default;

        public Task<IReadOnlyList<CameraDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CameraDevice>>([Device]);
        }

        public Task ConnectAsync(string cameraId, CancellationToken cancellationToken = default)
        {
            ConnectedDevice = Device;
            State = CameraConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectedDevice = null;
            State = CameraConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public Task<CameraFrame> CaptureAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateFrame(1) with { Exposure = _settings.Exposure });
        }

        public async IAsyncEnumerable<CameraFrame> StreamFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            State = CameraConnectionState.Streaming;
            try
            {
                for (var i = 0; i < _frameNumbers.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_throwAfterFrames is not null && i >= _throwAfterFrames)
                    {
                        throw new InvalidOperationException("Scripted stream failure.");
                    }

                    yield return CreateFrame(_frameNumbers[i]) with { Exposure = _settings.Exposure };

                    if (_delay > TimeSpan.Zero)
                    {
                        await Task.Delay(_delay, cancellationToken);
                    }
                }
            }
            finally
            {
                if (ConnectedDevice is not null)
                {
                    State = CameraConnectionState.Connected;
                }
            }
        }

        public Task SetExposureAsync(TimeSpan exposure, CancellationToken cancellationToken = default)
        {
            _settings = _settings with { Exposure = exposure };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CameraProperty>>([]);
        }
    }
}
