namespace DcamVision.Imaging.Diagnostics;

public sealed record SupportBundleResult
{
    public required Guid BundleId { get; init; }

    public required string BundlePath { get; init; }

    public required bool WasCanceled { get; init; }

    public IReadOnlyList<string> IncludedFiles { get; init; } = [];
}
