using DcamVision.Core;

namespace DcamVision.Imaging.Diagnostics;

public sealed record ApplicationDiagnosticsSnapshot
{
    public string SchemaVersion { get; init; } = "1.0";

    public required DateTimeOffset GeneratedAt { get; init; }

    public required ApplicationInfoDiagnostics Application { get; init; }

    public required RuntimeEnvironmentDiagnostics Runtime { get; init; }

    public required CameraDiagnostics Camera { get; init; }

    public required AcquisitionDiagnostics Acquisition { get; init; }

    public required ImagingDiagnostics Imaging { get; init; }

    public required HistoryDiagnostics History { get; init; }

    public required ExportDiagnostics Export { get; init; }

    public required HealthDiagnostics Health { get; init; }

    public IReadOnlyList<DiagnosticLogEntry> RecentErrors { get; init; } = [];
}

public sealed record ApplicationInfoDiagnostics(
    string ApplicationName,
    string ApplicationVersion,
    string AssemblyVersion,
    DateTimeOffset StartedAt,
    TimeSpan Uptime,
    int ProcessId);

public sealed record RuntimeEnvironmentDiagnostics(
    string DotNetVersion,
    string OsDescription,
    string ProcessArchitecture,
    long WorkingSetBytes,
    long ManagedMemoryBytes,
    int ProcessorCount);

public sealed record CameraDiagnostics(
    CameraBackend SelectedBackend,
    CameraBackend? ActiveBackend,
    bool SimulatorAvailable,
    bool DcamRuntimeAvailable,
    string? DcamUnavailableReason,
    string? DcamApiVersion,
    int DcamDeviceCount,
    bool DcamInitialized,
    string? ConnectedCamera,
    string? CameraId,
    CameraConnectionState State);

public sealed record AcquisitionDiagnostics(
    LiveAcquisitionState LiveState,
    Guid? SessionId,
    TimeSpan SessionDuration,
    double AcquisitionFps,
    double DisplayFps,
    long FramesAcquired,
    long FramesProcessed,
    long FramesDisplayed,
    long PipelineDrops,
    long SourceFrameGaps,
    int BufferOccupancy,
    int BufferCapacity);

public sealed record ImagingDiagnostics(
    long? CurrentFrameNumber,
    string? Resolution,
    TimeSpan? ProcessingTime,
    int HistogramBins,
    AutoContrastMode AutoContrastMode,
    ushort BlackPoint,
    ushort WhitePoint,
    double Gamma);

public sealed record HistoryDiagnostics(
    int CaptureCount,
    int CaptureCapacity,
    int SessionCount,
    long EstimatedMemoryBytes,
    string? ActiveSessionName);

public sealed record ExportDiagnostics(
    bool IsExporting,
    Guid? LastExportId,
    string? LastExportDestination,
    string LastExportResult,
    int LastExportCaptureCount,
    int LastExportFailureCount,
    TimeSpan? LastExportDuration);

public sealed record HealthDiagnostics(
    DiagnosticHealthState Application,
    DiagnosticHealthState CameraBackend,
    DiagnosticHealthState Acquisition,
    DiagnosticHealthState Export,
    int RecentErrorCount,
    DiagnosticHealthState Overall);
