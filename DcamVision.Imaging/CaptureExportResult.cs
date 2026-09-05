namespace DcamVision.Imaging;

public sealed record CaptureExportResult
{
    public required Guid ExportId { get; init; }

    public required int RequestedCaptureCount { get; init; }

    public required int ExportedCaptureCount { get; init; }

    public required int SkippedCaptureCount { get; init; }

    public required bool WasCanceled { get; init; }

    public required string OutputDirectory { get; init; }

    public IReadOnlyList<CaptureExportItemResult> Items { get; init; } = [];

    public string Summary => WasCanceled
        ? $"Export canceled after {ExportedCaptureCount} of {RequestedCaptureCount} capture(s)."
        : $"Exported {ExportedCaptureCount} of {RequestedCaptureCount} capture(s), skipped {SkippedCaptureCount}.";
}
