using DcamVision.Core;

namespace DcamVision.Imaging.Diagnostics;

public sealed record DiagnosticsState
{
    public CameraBackend SelectedBackend { get; init; } = CameraBackend.Auto;

    public CameraBackend? ActiveBackend { get; init; }

    public bool SimulatorAvailable { get; init; } = true;

    public bool DcamRuntimeAvailable { get; init; }

    public string? DcamUnavailableReason { get; init; }

    public string? DcamApiVersion { get; init; }

    public int DcamDeviceCount { get; init; }

    public bool DcamInitialized { get; init; }

    public string? ConnectedCamera { get; init; }

    public string? CameraId { get; init; }

    public CameraConnectionState CameraState { get; init; } = CameraConnectionState.Disconnected;

    public LiveAcquisitionMetrics AcquisitionMetrics { get; init; } = LiveAcquisitionMetrics.Empty(0);

    public LiveAcquisitionState LiveState { get; init; } = LiveAcquisitionState.Stopped;

    public Guid? LiveSessionId { get; init; }

    public long? CurrentFrameNumber { get; init; }

    public string? Resolution { get; init; }

    public TimeSpan? ProcessingTime { get; init; }

    public int HistogramBins { get; init; }

    public AutoContrastMode AutoContrastMode { get; init; } = AutoContrastMode.Off;

    public ushort BlackPoint { get; init; }

    public ushort WhitePoint { get; init; } = ushort.MaxValue;

    public double Gamma { get; init; } = 1.0;

    public int CaptureCount { get; init; }

    public int CaptureCapacity { get; init; }

    public int SessionCount { get; init; }

    public long EstimatedHistoryMemoryBytes { get; init; }

    public string? ActiveCaptureSessionName { get; init; }

    public bool IsExporting { get; init; }

    public Guid? LastExportId { get; init; }

    public string? LastExportDestination { get; init; }

    public string LastExportResult { get; init; } = "No export run.";

    public int LastExportCaptureCount { get; init; }

    public int LastExportFailureCount { get; init; }

    public TimeSpan? LastExportDuration { get; init; }
}
