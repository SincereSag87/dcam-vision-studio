using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed record CaptureMetadata
{
    public required string CameraId { get; init; }

    public required string CameraDisplayName { get; init; }

    public required string Manufacturer { get; init; }

    public required string Model { get; init; }

    public required string SerialNumber { get; init; }

    public required TimeSpan Exposure { get; init; }

    public required double Gain { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required CameraPixelFormat PixelFormat { get; init; }

    public required string TriggerMode { get; init; }

    public IReadOnlyDictionary<string, object?> PropertySnapshot { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
