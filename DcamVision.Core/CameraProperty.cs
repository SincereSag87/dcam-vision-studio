namespace DcamVision.Core;

public sealed record CameraProperty
{
    public required string Id { get; init; }

    public string Name => Id;

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public string Category { get; init; } = CameraPropertyCategories.Advanced;

    public int DisplayOrder { get; init; }

    public required CameraPropertyType PropertyType { get; init; }

    public required object Value { get; init; }

    public object? Minimum { get; init; }

    public object? Maximum { get; init; }

    public object? Step { get; init; }

    public string? Unit { get; init; }

    public CameraPropertyAccess Access { get; init; } = CameraPropertyAccess.Read | CameraPropertyAccess.Write;

    public IReadOnlyList<CameraPropertyOption> Options { get; init; } = [];

    public bool IsAvailable { get; init; } = true;

    public string? AvailabilityReason { get; init; }

    public bool IsReadOnly => !Access.HasFlag(CameraPropertyAccess.Write);

    public bool IsWritableWhileStreaming => Access.HasFlag(CameraPropertyAccess.WriteWhileStreaming);

    public string FormatValue()
    {
        var value = CameraPropertyValueConverter.Format(Value, PropertyType);
        return Unit is null ? value : $"{value} {Unit}";
    }
}
