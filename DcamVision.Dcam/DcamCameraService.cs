using DcamVision.Core;

namespace DcamVision.Dcam;

public sealed class DcamCameraService : ICameraService
{
    public CameraConnectionState State => CameraConnectionState.Disconnected;

    public CameraDevice? ConnectedDevice => null;

    public CaptureSettings CurrentSettings { get; } = new();

    public CameraExposureRange ExposureRange { get; } = CameraExposureRange.Default;

    public Task<IReadOnlyList<CameraDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    public Task ConnectAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    public Task<CameraFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    public IAsyncEnumerable<CameraFrame> StreamFramesAsync(CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    public Task SetExposureAsync(TimeSpan exposure, CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    public Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        throw CreateDeferredException();
    }

    private static NotImplementedException CreateDeferredException()
    {
        return new NotImplementedException("Hamamatsu DCAM integration is deferred to Phase 9.");
    }
}
