namespace DcamVision.Imaging;

public static class CaptureExportValidator
{
    public static void Validate(CaptureExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Captures);
        ArgumentNullException.ThrowIfNull(request.Options);

        if (request.Captures.Count == 0)
        {
            throw new ArgumentException("Select at least one capture to export.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new ArgumentException("Export destination is required.", nameof(request));
        }

        if (request.Options.EnabledFormats().Count == 0)
        {
            throw new ArgumentException("Select at least one export format.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Options.FilenameTemplate))
        {
            throw new ArgumentException("Filename template is required.", nameof(request));
        }

    }
}
