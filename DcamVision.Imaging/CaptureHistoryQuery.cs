namespace DcamVision.Imaging;

public static class CaptureHistoryQuery
{
    public static IReadOnlyList<CaptureRecord> Apply(
        IEnumerable<CaptureRecord> captures,
        IEnumerable<CaptureSession> sessions,
        CaptureHistoryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(captures);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(filter);

        var sessionLookup = sessions.ToDictionary(session => session.SessionId);
        var query = filter.SearchText.Trim();
        var filtered = captures.Where(capture =>
            (filter.SessionId is null || capture.SessionId == filter.SessionId) &&
            (filter.Source is null || capture.Source == filter.Source) &&
            (!filter.TaggedOnly || capture.Tags.Count > 0) &&
            (!filter.SaturatedOnly || capture.Statistics.SaturationPercentage > 0) &&
            (query.Length == 0 || MatchesSearch(capture, sessionLookup, query)));

        return Sort(filtered, filter.SortMode).ToArray();
    }

    private static bool MatchesSearch(
        CaptureRecord capture,
        IReadOnlyDictionary<Guid, CaptureSession> sessions,
        string query)
    {
        sessions.TryGetValue(capture.SessionId, out var session);
        return capture.CaptureId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               capture.Notes.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               capture.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               capture.Metadata.Model.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               capture.Metadata.SerialNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               capture.Source.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (session?.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static IOrderedEnumerable<CaptureRecord> Sort(
        IEnumerable<CaptureRecord> captures,
        CaptureHistorySortMode sortMode)
    {
        return sortMode switch
        {
            CaptureHistorySortMode.OldestFirst => captures.OrderBy(capture => capture.CapturedAt).ThenBy(capture => capture.SequenceNumber),
            CaptureHistorySortMode.ExposureLowToHigh => captures.OrderBy(capture => capture.Metadata.Exposure).ThenBy(capture => capture.SequenceNumber),
            CaptureHistorySortMode.ExposureHighToLow => captures.OrderByDescending(capture => capture.Metadata.Exposure).ThenBy(capture => capture.SequenceNumber),
            CaptureHistorySortMode.MeanIntensity => captures.OrderByDescending(capture => capture.Statistics.Mean).ThenBy(capture => capture.SequenceNumber),
            CaptureHistorySortMode.SaturationPercentage => captures.OrderByDescending(capture => capture.Statistics.SaturationPercentage).ThenBy(capture => capture.SequenceNumber),
            _ => captures.OrderByDescending(capture => capture.CapturedAt).ThenByDescending(capture => capture.SequenceNumber)
        };
    }
}
