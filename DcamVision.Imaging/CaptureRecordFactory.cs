using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed class CaptureRecordFactory
{
    private readonly ICameraService _cameraService;

    public CaptureRecordFactory(ICameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task<CaptureRecord> CreateAsync(
        Guid sessionId,
        long sequenceNumber,
        CameraFrame frame,
        CaptureSource source,
        FrameStatistics? statistics = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        var frameSnapshot = CloneFrame(frame);
        var metadata = await CreateMetadataAsync(frameSnapshot, cancellationToken).ConfigureAwait(false);
        return new CaptureRecord
        {
            CaptureId = Guid.NewGuid(),
            SessionId = sessionId,
            SequenceNumber = sequenceNumber,
            Frame = frameSnapshot,
            CapturedAt = DateTimeOffset.UtcNow,
            Source = source,
            Metadata = metadata,
            Statistics = statistics ?? FrameStatisticsCalculator.Calculate(frameSnapshot)
        };
    }

    public static CameraFrame CloneFrame(CameraFrame frame)
    {
        return frame with { Pixels = frame.Pixels.ToArray() };
    }

    private async Task<CaptureMetadata> CreateMetadataAsync(CameraFrame frame, CancellationToken cancellationToken)
    {
        var device = _cameraService.ConnectedDevice;
        var settings = _cameraService.CurrentSettings;
        var properties = device is null
            ? []
            : await _cameraService.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);

        var propertySnapshot = properties.ToDictionary(
            property => property.Id,
            property => (object?)property.Value,
            StringComparer.OrdinalIgnoreCase);

        return new CaptureMetadata
        {
            CameraId = device?.Id ?? "unknown",
            CameraDisplayName = device?.DisplayName ?? "Unknown Camera",
            Manufacturer = device?.Manufacturer ?? string.Empty,
            Model = device?.Model ?? string.Empty,
            SerialNumber = device?.SerialNumber ?? string.Empty,
            Exposure = frame.Exposure,
            Gain = settings.Gain,
            Width = frame.Width,
            Height = frame.Height,
            PixelFormat = frame.PixelFormat,
            TriggerMode = settings.TriggerMode,
            PropertySnapshot = propertySnapshot
        };
    }
}
