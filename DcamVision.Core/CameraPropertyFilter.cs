namespace DcamVision.Core;

public sealed record CameraPropertyFilter(
    string SearchText = "",
    string Category = "All",
    bool WritableOnly = false);

public static class CameraPropertyFilterEngine
{
    public static IReadOnlyList<CameraProperty> Apply(
        IEnumerable<CameraProperty> properties,
        CameraPropertyFilter filter)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(filter);

        var query = filter.SearchText.Trim();
        return properties
            .Where(property =>
                (filter.Category == "All" || property.Category == filter.Category) &&
                (!filter.WritableOnly || !property.IsReadOnly) &&
                (query.Length == 0 ||
                 property.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 property.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 (property.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                 property.Category.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
