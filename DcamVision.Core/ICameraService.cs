namespace DcamVision.Core;

public interface ICameraService
{
    CameraConnectionState State { get; }

    CameraDevice? ConnectedDevice { get; }

    CaptureSettings CurrentSettings { get; }

    CameraExposureRange ExposureRange { get; }

    Task<IReadOnlyList<CameraDevice>> DiscoverAsync(CancellationToken cancellationToken = default);

    Task ConnectAsync(string cameraId, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<CameraFrame> CaptureAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<CameraFrame> StreamFramesAsync(CancellationToken cancellationToken = default);

    Task SetExposureAsync(TimeSpan exposure, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default);
}
