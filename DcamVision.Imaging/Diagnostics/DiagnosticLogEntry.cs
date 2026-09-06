using Microsoft.Extensions.Logging;

namespace DcamVision.Imaging.Diagnostics;

public sealed record DiagnosticLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required LogLevel Level { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public int EventId { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }

    public string? StackTrace { get; init; }

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    public bool IsError => Level >= LogLevel.Warning;
}
