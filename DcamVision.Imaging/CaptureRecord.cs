using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed record CaptureRecord
{
    public required Guid CaptureId { get; init; }

    public required Guid SessionId { get; init; }

    public required long SequenceNumber { get; init; }

    public required CameraFrame Frame { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public required CaptureSource Source { get; init; }

    public required CaptureMetadata Metadata { get; init; }

    public required FrameStatistics Statistics { get; init; }

    public string Notes { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];
}
