using Microsoft.Extensions.Logging;

namespace DcamVision.Imaging.Diagnostics;

public sealed record DiagnosticsLoggingOptions
{
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    public int RetainedFileDays { get; init; } = 14;

    public int InMemoryCapacity { get; init; } = 1000;

    public int RecentErrorCapacity { get; init; } = 100;

    public string? LogDirectory { get; init; }

    public string ApplicationName { get; init; } = "DCAMVisionStudio";

    public void Validate()
    {
        if (RetainedFileDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RetainedFileDays), "Log retention must be at least one day.");
        }

        if (InMemoryCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(InMemoryCapacity), "In-memory log capacity must be positive.");
        }

        if (RecentErrorCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RecentErrorCapacity), "Recent error capacity must be positive.");
        }
    }
}
