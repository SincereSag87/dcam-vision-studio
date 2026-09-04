using DcamVision.Core;

namespace DcamVision.Tests;

public sealed class SimulatedCameraServiceTests
{
    [Fact]
    public async Task DiscoverAsync_ReturnsSimulatedCamera()
    {
        var service = new SimulatedCameraService();

        var cameras = await service.DiscoverAsync();

        var camera = Assert.Single(cameras);
        Assert.True(camera.IsSimulated);
        Assert.Equal("simulated-hamamatsu-orca", camera.Id);
    }

    [Fact]
    public async Task ConnectAndDisconnect_UpdateState()
    {
        var service = new SimulatedCameraService();
        var camera = Assert.Single(await service.DiscoverAsync());

        await service.ConnectAsync(camera.Id);

        Assert.Equal(CameraConnectionState.Connected, service.State);
        Assert.Equal(camera, service.ConnectedDevice);

        await service.DisconnectAsync();

        Assert.Equal(CameraConnectionState.Disconnected, service.State);
        Assert.Null(service.ConnectedDevice);
    }

    [Fact]
    public async Task CaptureBeforeConnection_Fails()
    {
        var service = new SimulatedCameraService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CaptureAsync());

        Assert.Contains("No camera is connected", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureAsync_ReturnsValidFrame()
    {
        var service = await CreateConnectedServiceAsync();

        var frame = await service.CaptureAsync();

        Assert.Equal(CameraPixelFormat.Mono16, frame.PixelFormat);
        Assert.Equal(1, frame.FrameNumber);
        Assert.Equal(service.CurrentSettings.Exposure, frame.Exposure);
        Assert.All(frame.Pixels, pixel => Assert.InRange(pixel, ushort.MinValue, ushort.MaxValue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.05)]
    [InlineData(10_001)]
    public async Task SetExposureAsync_RejectsInvalidExposure(double milliseconds)
    {
        var service = new SimulatedCameraService();
        var exposure = TimeSpan.FromMilliseconds(milliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetExposureAsync(exposure));
    }

    [Fact]
    public async Task SetExposureAsync_AcceptsValidExposure()
    {
        var service = new SimulatedCameraService();
        var exposure = TimeSpan.FromMilliseconds(42);

        await service.SetExposureAsync(exposure);

        Assert.Equal(exposure, service.CurrentSettings.Exposure);
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("abc", "number")]
    [InlineData("0.05", "at least")]
    [InlineData("10001", "or less")]
    public void CameraExposureRange_RejectsInvalidText(string value, string expectedMessageFragment)
    {
        var isValid = CameraExposureRange.TryParseMilliseconds(value, out _, out var errorMessage);

        Assert.False(isValid);
        Assert.Contains(expectedMessageFragment, errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CameraExposureRange_ParsesValidExposureText()
    {
        var isValid = CameraExposureRange.TryParseMilliseconds("25.5", out var exposure, out var errorMessage);

        Assert.True(isValid);
        Assert.Equal(TimeSpan.FromMilliseconds(25.5), exposure);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    public async Task GeneratedFrame_HasConfiguredDimensions()
    {
        var service = await CreateConnectedServiceAsync();

        var frame = await service.CaptureAsync();

        Assert.Equal(service.CurrentSettings.Width, frame.Width);
        Assert.Equal(service.CurrentSettings.Height, frame.Height);
    }

    [Fact]
    public async Task GeneratedFrame_HasExpectedPixelBufferSize()
    {
        var service = await CreateConnectedServiceAsync();

        var frame = await service.CaptureAsync();

        Assert.Equal(frame.Width * frame.Height, frame.Pixels.Length);
    }

    [Fact]
    public async Task StreamFramesAsync_ProducesSequentialFrames()
    {
        var service = await CreateConnectedServiceAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var frames = new List<CameraFrame>();

        await foreach (var frame in service.StreamFramesAsync(cancellation.Token))
        {
            frames.Add(frame);
            if (frames.Count == 3)
            {
                break;
            }
        }

        Assert.Equal([1, 2, 3], frames.Select(frame => frame.FrameNumber).ToArray());
        Assert.Equal(CameraConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task StreamFramesAsync_HonorsCancellation()
    {
        var service = await CreateConnectedServiceAsync();
        using var cancellation = new CancellationTokenSource();
        var receivedFrame = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var frame in service.StreamFramesAsync(cancellation.Token))
            {
                receivedFrame = frame.FrameNumber > 0;
                await cancellation.CancelAsync();
            }
        });

        Assert.True(receivedFrame);
    }

    [Fact]
    public async Task GetPropertiesAsync_ReturnsExpectedCameraProperties()
    {
        var service = new SimulatedCameraService();

        var properties = await service.GetPropertiesAsync();

        Assert.Contains(properties, property => property.DisplayName == "Exposure Time");
        Assert.Contains(properties, property => property.DisplayName == "Gain");
        Assert.Contains(properties, property => property.DisplayName == "Width");
        Assert.Contains(properties, property => property.DisplayName == "Height");
        Assert.Contains(properties, property => property.DisplayName == "Pixel Format");
        Assert.Contains(properties, property => property.DisplayName == "Trigger Mode");
    }

    private static async Task<SimulatedCameraService> CreateConnectedServiceAsync()
    {
        var service = new SimulatedCameraService();
        var camera = Assert.Single(await service.DiscoverAsync());
        await service.ConnectAsync(camera.Id);
        return service;
    }
}
