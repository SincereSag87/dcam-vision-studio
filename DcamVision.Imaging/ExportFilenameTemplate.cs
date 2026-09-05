using DcamVision.Core;

namespace DcamVision.Imaging;

public static class ExportFilenameTemplate
{
    public static string Expand(
        string template,
        CaptureRecord capture,
        CaptureSession? session,
        int captureIndex)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("Filename template is required.", nameof(template));
        }

        var local = capture.CapturedAt.ToLocalTime();
        var expanded = template
            .Replace("{captureId}", capture.CaptureId.ToString("N"), StringComparison.OrdinalIgnoreCase)
            .Replace("{session}", session?.Name ?? "Session", StringComparison.OrdinalIgnoreCase)
            .Replace("{timestamp}", local.ToString("yyyyMMdd_HHmmss_fff"), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", local.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", local.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{source}", capture.Source.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{exposure}", ExposureUnitConverter.Format(capture.Metadata.Exposure).Replace(" ", ""), StringComparison.OrdinalIgnoreCase)
            .Replace("{frameNumber}", capture.Frame.FrameNumber.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{cameraModel}", capture.Metadata.Model, StringComparison.OrdinalIgnoreCase)
            .Replace("{captureIndex}", captureIndex.ToString("0000"), StringComparison.OrdinalIgnoreCase);

        return ExportFilenameSanitizer.SanitizeComponent(expanded);
    }
}
