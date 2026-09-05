namespace DcamVision.Imaging;

public sealed record CaptureExportItemResult
{
    public required Guid CaptureId { get; init; }

    public required bool Success { get; init; }

    public bool Skipped { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<CaptureExportFileResult> Files { get; init; } = [];
}
