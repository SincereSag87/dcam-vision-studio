namespace DcamVision.Imaging;

public static class CaptureExportSizeEstimator
{
    public static long EstimateBytes(CaptureExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var formats = request.Options.EnabledFormats();
        long total = 0;
        foreach (var capture in request.Captures)
        {
            var rawBytes = (long)capture.Frame.Width * capture.Frame.Height * sizeof(ushort);
            if (formats.Contains(CaptureExportFormat.Tiff16))
            {
                total += rawBytes + 256;
            }

            if (formats.Contains(CaptureExportFormat.RawMono16))
            {
                total += rawBytes;
            }

            if (formats.Contains(CaptureExportFormat.PngPreview))
            {
                total += Math.Max(1024, rawBytes / 2);
            }

            if (formats.Contains(CaptureExportFormat.JsonMetadata))
            {
                total += 4096;
            }
        }

        if (request.Options.CreateManifest)
        {
            total += 4096 + request.Captures.Count * 512L;
        }

        return total;
    }
}
