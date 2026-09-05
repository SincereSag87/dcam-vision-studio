namespace DcamVision.Imaging;

public sealed record CaptureExportRequest
{
    public required IReadOnlyList<CaptureRecord> Captures { get; init; }

    public required IReadOnlyList<CaptureSession> Sessions { get; init; }

    public required string OutputDirectory { get; init; }

    public required CaptureExportOptions Options { get; init; }
}
