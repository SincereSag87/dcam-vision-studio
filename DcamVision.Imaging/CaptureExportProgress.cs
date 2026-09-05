namespace DcamVision.Imaging;

public sealed record CaptureExportProgress
{
    public required int CompletedCaptures { get; init; }

    public required int TotalCaptures { get; init; }

    public string? CurrentCaptureName { get; init; }

    public CaptureExportFormat? CurrentFormat { get; init; }

    public double Percentage => TotalCaptures == 0 ? 0 : CompletedCaptures * 100.0 / TotalCaptures;
}
