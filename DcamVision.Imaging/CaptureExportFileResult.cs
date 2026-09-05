namespace DcamVision.Imaging;

public sealed record CaptureExportFileResult(
    CaptureExportFormat Format,
    string RelativePath,
    long BytesWritten,
    string? Sha256);
