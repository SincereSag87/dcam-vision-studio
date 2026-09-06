using Microsoft.Extensions.Logging;

namespace DcamVision.Imaging.Diagnostics;

public sealed record DiagnosticLogFilter(
    LogLevel MinimumLevel = LogLevel.Information,
    string Category = "",
    string SearchText = "");
