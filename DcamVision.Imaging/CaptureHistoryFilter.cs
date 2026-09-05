namespace DcamVision.Imaging;

public sealed record CaptureHistoryFilter
{
    public string SearchText { get; init; } = string.Empty;

    public Guid? SessionId { get; init; }

    public CaptureSource? Source { get; init; }

    public bool TaggedOnly { get; init; }

    public bool SaturatedOnly { get; init; }

    public CaptureHistorySortMode SortMode { get; init; } = CaptureHistorySortMode.NewestFirst;
}
