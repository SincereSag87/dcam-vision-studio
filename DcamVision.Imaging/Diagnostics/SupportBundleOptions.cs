namespace DcamVision.Imaging.Diagnostics;

public sealed record SupportBundleOptions
{
    public bool IncludeCameraSerial { get; init; }

    public bool IncludeFullPaths { get; init; }

    public bool IncludeDebugLogs { get; init; }

    public string OutputDirectory { get; init; } = DiagnosticsPaths.DefaultSupportBundleDirectory();
}
