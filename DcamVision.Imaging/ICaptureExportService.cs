namespace DcamVision.Imaging;

public interface ICaptureExportService
{
    Task<CaptureExportResult> ExportAsync(
        CaptureExportRequest request,
        IProgress<CaptureExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
