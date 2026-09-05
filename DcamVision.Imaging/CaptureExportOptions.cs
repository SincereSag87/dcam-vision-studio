namespace DcamVision.Imaging;

public sealed record CaptureExportOptions
{
    public const string DefaultFilenameTemplate = "{session}_{timestamp}_{captureIndex}_{exposure}";

    public bool ExportTiff16 { get; init; } = true;

    public bool ExportPngPreview { get; init; } = true;

    public bool ExportRawMono16 { get; init; }

    public bool ExportJsonMetadata { get; init; } = true;

    public bool CreateManifest { get; init; } = true;

    public string FilenameTemplate { get; init; } = DefaultFilenameTemplate;

    public ExportCollisionBehavior CollisionBehavior { get; init; } = ExportCollisionBehavior.Rename;

    public ExportDirectoryLayout DirectoryLayout { get; init; } = ExportDirectoryLayout.BySession;

    public ImageDisplaySettings PreviewDisplaySettings { get; init; } = ImageDisplaySettings.Default;

    public IReadOnlyList<CaptureExportFormat> EnabledFormats()
    {
        var formats = new List<CaptureExportFormat>();
        if (ExportTiff16)
        {
            formats.Add(CaptureExportFormat.Tiff16);
        }

        if (ExportPngPreview)
        {
            formats.Add(CaptureExportFormat.PngPreview);
        }

        if (ExportRawMono16)
        {
            formats.Add(CaptureExportFormat.RawMono16);
        }

        if (ExportJsonMetadata)
        {
            formats.Add(CaptureExportFormat.JsonMetadata);
        }

        return formats;
    }
}
