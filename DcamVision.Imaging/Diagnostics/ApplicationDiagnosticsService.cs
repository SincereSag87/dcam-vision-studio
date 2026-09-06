using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DcamVision.Imaging.Diagnostics;

public sealed class ApplicationDiagnosticsService : IApplicationDiagnosticsService
{
    private readonly InMemoryDiagnosticLogStore _logStore;
    private readonly DateTimeOffset _startedAt;
    private readonly string _applicationName;

    public ApplicationDiagnosticsService(InMemoryDiagnosticLogStore logStore, string applicationName = "DCAM Vision Studio")
    {
        _logStore = logStore;
        _applicationName = applicationName;
        _startedAt = DateTimeOffset.UtcNow;
    }

    public ApplicationDiagnosticsSnapshot CreateSnapshot(DiagnosticsState state)
    {
        var now = DateTimeOffset.UtcNow;
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ApplicationDiagnosticsService).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            assembly.GetName().Version?.ToString() ??
            "unknown";
        var acquisitionState = state.LiveState == LiveAcquisitionState.Faulted
            ? DiagnosticHealthState.Faulted
            : state.AcquisitionMetrics.TotalDropped > 0 ? DiagnosticHealthState.Degraded : DiagnosticHealthState.Healthy;
        var cameraState = state.SelectedBackend == Core.CameraBackend.HamamatsuDcam && !state.DcamRuntimeAvailable
            ? DiagnosticHealthState.Warning
            : DiagnosticHealthState.Healthy;
        var exportState = state.LastExportFailureCount > 0 ? DiagnosticHealthState.Warning : DiagnosticHealthState.Healthy;
        var recentErrors = _logStore.RecentErrors;
        var overall = new[] { cameraState, acquisitionState, exportState }.Contains(DiagnosticHealthState.Faulted)
            ? DiagnosticHealthState.Faulted
            : new[] { cameraState, acquisitionState, exportState }.Any(state => state is DiagnosticHealthState.Warning or DiagnosticHealthState.Degraded)
                ? DiagnosticHealthState.Warning
                : DiagnosticHealthState.Healthy;

        return new ApplicationDiagnosticsSnapshot
        {
            GeneratedAt = now,
            Application = new ApplicationInfoDiagnostics(
                _applicationName,
                version,
                assembly.GetName().Version?.ToString() ?? "unknown",
                _startedAt,
                now - _startedAt,
                Environment.ProcessId),
            Runtime = new RuntimeEnvironmentDiagnostics(
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.WorkingSet,
                GC.GetTotalMemory(forceFullCollection: false),
                Environment.ProcessorCount),
            Camera = new CameraDiagnostics(
                state.SelectedBackend,
                state.ActiveBackend,
                state.SimulatorAvailable,
                state.DcamRuntimeAvailable,
                state.DcamUnavailableReason,
                state.DcamApiVersion,
                state.DcamDeviceCount,
                state.DcamInitialized,
                state.ConnectedCamera,
                state.CameraId,
                state.CameraState),
            Acquisition = new AcquisitionDiagnostics(
                state.LiveState,
                state.LiveSessionId,
                state.AcquisitionMetrics.Elapsed,
                state.AcquisitionMetrics.AcquisitionFps,
                state.AcquisitionMetrics.DisplayFps,
                state.AcquisitionMetrics.FramesAcquired,
                state.AcquisitionMetrics.FramesProcessed,
                state.AcquisitionMetrics.FramesDisplayed,
                state.AcquisitionMetrics.PipelineDrops,
                state.AcquisitionMetrics.SourceFrameGaps,
                state.AcquisitionMetrics.BufferOccupancy,
                state.AcquisitionMetrics.BufferCapacity),
            Imaging = new ImagingDiagnostics(
                state.CurrentFrameNumber,
                state.Resolution,
                state.ProcessingTime,
                state.HistogramBins,
                state.AutoContrastMode,
                state.BlackPoint,
                state.WhitePoint,
                state.Gamma),
            History = new HistoryDiagnostics(
                state.CaptureCount,
                state.CaptureCapacity,
                state.SessionCount,
                state.EstimatedHistoryMemoryBytes,
                state.ActiveCaptureSessionName),
            Export = new ExportDiagnostics(
                state.IsExporting,
                state.LastExportId,
                Redactor.Redact(state.LastExportDestination ?? string.Empty),
                state.LastExportResult,
                state.LastExportCaptureCount,
                state.LastExportFailureCount,
                state.LastExportDuration),
            Health = new HealthDiagnostics(
                DiagnosticHealthState.Healthy,
                cameraState,
                acquisitionState,
                exportState,
                recentErrors.Count,
                overall),
            RecentErrors = recentErrors
        };
    }

    public string CreateTextReport(ApplicationDiagnosticsSnapshot snapshot, SupportBundleOptions? options = null)
    {
        options ??= new SupportBundleOptions();
        var serial = options.IncludeCameraSerial ? snapshot.Camera.CameraId : "redacted";
        return string.Join(Environment.NewLine, [
            $"{snapshot.Application.ApplicationName} {snapshot.Application.ApplicationVersion}",
            $"Generated: {snapshot.GeneratedAt:O}",
            $".NET: {snapshot.Runtime.DotNetVersion}",
            $"OS: {snapshot.Runtime.OsDescription}",
            $"Architecture: {snapshot.Runtime.ProcessArchitecture}",
            $"Health: {snapshot.Health.Overall}",
            $"Backend: {snapshot.Camera.SelectedBackend}",
            $"DCAM Runtime: {(snapshot.Camera.DcamRuntimeAvailable ? "Available" : "Unavailable")}",
            $"Camera: {snapshot.Camera.ConnectedCamera ?? "None"}",
            $"Camera ID: {serial}",
            $"Camera State: {snapshot.Camera.State}",
            $"Acquisition: {snapshot.Acquisition.LiveState}",
            $"Acquisition FPS: {snapshot.Acquisition.AcquisitionFps:0.0}",
            $"Display FPS: {snapshot.Acquisition.DisplayFps:0.0}",
            $"Pipeline Drops: {snapshot.Acquisition.PipelineDrops}",
            $"Source Gaps: {snapshot.Acquisition.SourceFrameGaps}",
            $"History Captures: {snapshot.History.CaptureCount}/{snapshot.History.CaptureCapacity}",
            $"History Sessions: {snapshot.History.SessionCount}",
            $"Export: {snapshot.Export.LastExportResult}",
            $"Recent Errors: {snapshot.Health.RecentErrorCount}"
        ]);
    }
}
