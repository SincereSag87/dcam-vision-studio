namespace DcamVision.Core;

public sealed record CameraProperty(
    string Name,
    string DisplayName,
    object Value,
    string? Unit = null,
    object? Minimum = null,
    object? Maximum = null,
    bool IsReadOnly = false);
