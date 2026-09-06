using System.IO.Compression;
using DcamVision.Core;
using DcamVision.Imaging;
using DcamVision.Imaging.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DcamVision.Tests;

public sealed class DiagnosticsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "dcam-diagnostics-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void InMemoryDiagnosticLogStore_EnforcesCapacity()
    {
        var store = new InMemoryDiagnosticLogStore(capacity: 2, errorCapacity: 10);

        store.Add(CreateEntry("one"));
        store.Add(CreateEntry("two"));
        store.Add(CreateEntry("three"));

        Assert.Equal(["two", "three"], store.Entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void InMemoryDiagnosticLogStore_EnforcesRecentErrorCapacity()
    {
        var store = new InMemoryDiagnosticLogStore(capacity: 10, errorCapacity: 1);

        store.Add(CreateEntry("warning", LogLevel.Warning));
        store.Add(CreateEntry("error", LogLevel.Error));

        var error = Assert.Single(store.RecentErrors);
        Assert.Equal("error", error.Message);
    }

    [Fact]
    public void DiagnosticLogFilter_FiltersByLevel()
    {
        var store = new InMemoryDiagnosticLogStore();
        store.Add(CreateEntry("info", LogLevel.Information));
        store.Add(CreateEntry("warn", LogLevel.Warning));

        var result = store.Query(new DiagnosticLogFilter(LogLevel.Warning));

        var entry = Assert.Single(result);
        Assert.Equal("warn", entry.Message);
    }

    [Fact]
    public void DiagnosticLogFilter_FiltersByCategory()
    {
        var store = new InMemoryDiagnosticLogStore();
        store.Add(CreateEntry("camera", category: "Camera"));
        store.Add(CreateEntry("export", category: "Export"));

        var result = store.Query(new DiagnosticLogFilter(Category: "cam"));

        var entry = Assert.Single(result);
        Assert.Equal("Camera", entry.Category);
    }

    [Fact]
    public void DiagnosticLogFilter_SearchesMessageAndProperties()
    {
        var store = new InMemoryDiagnosticLogStore();
        store.Add(CreateEntry("connected", properties: new Dictionary<string, string> { ["CameraId"] = "CAM-42" }));
        store.Add(CreateEntry("exported"));

        var result = store.Query(new DiagnosticLogFilter(SearchText: "cam-42"));

        var entry = Assert.Single(result);
        Assert.Equal("connected", entry.Message);
    }

    [Fact]
    public void InMemoryDiagnosticLogStore_ClearRemovesEntriesAndErrors()
    {
        var store = new InMemoryDiagnosticLogStore();
        store.Add(CreateEntry("error", LogLevel.Error));

        store.Clear();

        Assert.Empty(store.Entries);
        Assert.Empty(store.RecentErrors);
    }

    [Fact]
    public void DiagnosticsLoggerProvider_WritesStructuredPropertiesToMemory()
    {
        using var provider = CreateProvider(out var store, out _);
        var logger = provider.CreateLogger("Camera");

        logger.LogInformation("Camera connected {CameraId} using {Backend}", "CAM-1", "Simulator");

        var entry = Assert.Single(store.Entries);
        Assert.Equal("Camera", entry.Category);
        Assert.Equal("CAM-1", entry.Properties["CameraId"]);
        Assert.Equal("Simulator", entry.Properties["Backend"]);
    }

    [Fact]
    public void DiagnosticsLoggerProvider_WritesRollingFileLog()
    {
        using var provider = CreateProvider(out _, out var logDirectory);
        var logger = provider.CreateLogger("Export");

        logger.LogWarning("Export partial failure {ExportId}", "EXP-1");

        var logFile = Assert.Single(Directory.EnumerateFiles(logDirectory, "dcam-vision-studio-*.log"));
        var text = File.ReadAllText(logFile);
        Assert.Contains("Export partial failure", text, StringComparison.Ordinal);
        Assert.Contains("ExportId=EXP-1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsLoggerProvider_PrunesOldLogs()
    {
        var logDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "logs")).FullName;
        var oldFile = Path.Combine(logDirectory, "dcam-vision-studio-2000-01-01.log");
        File.WriteAllText(oldFile, "old");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-30));

        using var _ = new DiagnosticsLoggerProvider(new InMemoryDiagnosticLogStore(), new DiagnosticsLoggingOptions { LogDirectory = logDirectory, RetainedFileDays = 7 });

        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public void DiagnosticsLoggerProvider_IsThreadSafeForConcurrentWrites()
    {
        using var provider = CreateProvider(out var store, out _);
        var logger = provider.CreateLogger("Concurrent");

        Parallel.For(0, 50, index => logger.LogInformation("Message {Index}", index));

        Assert.Equal(50, store.Entries.Count);
    }

    [Fact]
    public void Redactor_ReplacesUserProfilePaths()
    {
        var redacted = Redactor.Redact(@"C:\Users\Raymond\AppData\Local\DCAMVisionStudio\Logs\a.log");

        Assert.DoesNotContain("Raymond", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationDiagnosticsService_SimulatorWithoutDcamRuntimeIsHealthy()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());

        var snapshot = service.CreateSnapshot(new DiagnosticsState
        {
            SelectedBackend = CameraBackend.Simulator,
            DcamRuntimeAvailable = false
        });

        Assert.Equal(DiagnosticHealthState.Healthy, snapshot.Health.Overall);
    }

    [Fact]
    public void ApplicationDiagnosticsService_DcamSelectedRuntimeMissingIsWarning()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());

        var snapshot = service.CreateSnapshot(new DiagnosticsState
        {
            SelectedBackend = CameraBackend.HamamatsuDcam,
            DcamRuntimeAvailable = false
        });

        Assert.Equal(DiagnosticHealthState.Warning, snapshot.Health.CameraBackend);
    }

    [Fact]
    public void ApplicationDiagnosticsService_DisconnectedCameraIsHealthy()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());

        var snapshot = service.CreateSnapshot(new DiagnosticsState
        {
            CameraState = CameraConnectionState.Disconnected
        });

        Assert.Equal(DiagnosticHealthState.Healthy, snapshot.Health.Overall);
    }

    [Fact]
    public void ApplicationDiagnosticsService_AcquisitionDropsAreDegraded()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());

        var snapshot = service.CreateSnapshot(new DiagnosticsState
        {
            AcquisitionMetrics = new LiveAcquisitionMetrics(10, 10, 5, 2, 0, 30, 15, TimeSpan.FromSeconds(1), 10, 0, 4)
        });

        Assert.Equal(DiagnosticHealthState.Degraded, snapshot.Health.Acquisition);
    }

    [Fact]
    public void ApplicationDiagnosticsService_PipelineFaultIsFaulted()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());

        var snapshot = service.CreateSnapshot(new DiagnosticsState
        {
            LiveState = LiveAcquisitionState.Faulted
        });

        Assert.Equal(DiagnosticHealthState.Faulted, snapshot.Health.Acquisition);
    }

    [Fact]
    public void ApplicationDiagnosticsService_RecentExportFailureIsWarning()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());

        var snapshot = service.CreateSnapshot(new DiagnosticsState
        {
            LastExportFailureCount = 1
        });

        Assert.Equal(DiagnosticHealthState.Warning, snapshot.Health.Export);
    }

    [Fact]
    public void ApplicationDiagnosticsService_CreatesTextReportWithRedactedSerialByDefault()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());
        var snapshot = service.CreateSnapshot(new DiagnosticsState { CameraId = "SERIAL-123", ConnectedCamera = "ORCA" });

        var report = service.CreateTextReport(snapshot);

        Assert.Contains("Camera ID: redacted", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SERIAL-123", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationDiagnosticsService_TextReportCanIncludeCameraSerial()
    {
        var service = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());
        var snapshot = service.CreateSnapshot(new DiagnosticsState { CameraId = "SERIAL-123", ConnectedCamera = "ORCA" });

        var report = service.CreateTextReport(snapshot, new SupportBundleOptions { IncludeCameraSerial = true });

        Assert.Contains("SERIAL-123", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupportBundleService_CreatesZipWithExpectedFiles()
    {
        var service = CreateSupportBundleService(out var diagnostics, out var store, out var logDirectory);
        store.Add(CreateEntry("error", LogLevel.Error));
        File.WriteAllText(Path.Combine(logDirectory, "dcam-vision-studio-2026-09-05.log"), "log");
        var snapshot = diagnostics.CreateSnapshot(new DiagnosticsState { CameraId = "SERIAL-1" });

        var result = await service.CreateAsync(snapshot, new SupportBundleOptions { OutputDirectory = _tempRoot });

        Assert.False(result.WasCanceled);
        Assert.True(File.Exists(result.BundlePath));
        using var archive = ZipFile.OpenRead(result.BundlePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "diagnostics.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "diagnostics.txt");
        Assert.Contains(archive.Entries, entry => entry.FullName == "recent-errors.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
    }

    [Fact]
    public async Task SupportBundleService_RedactsCameraSerialByDefault()
    {
        var service = CreateSupportBundleService(out var diagnostics, out _, out _);
        var snapshot = diagnostics.CreateSnapshot(new DiagnosticsState { CameraId = "SERIAL-PRIVATE" });

        var result = await service.CreateAsync(snapshot, new SupportBundleOptions { OutputDirectory = _tempRoot });

        using var archive = ZipFile.OpenRead(result.BundlePath);
        var entry = archive.GetEntry("diagnostics.txt");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry.Open());
        var text = await reader.ReadToEndAsync();
        Assert.DoesNotContain("SERIAL-PRIVATE", text, StringComparison.Ordinal);
        Assert.Contains("Camera ID: redacted", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupportBundleService_CanIncludeCameraSerial()
    {
        var service = CreateSupportBundleService(out var diagnostics, out _, out _);
        var snapshot = diagnostics.CreateSnapshot(new DiagnosticsState { CameraId = "SERIAL-INCLUDED" });

        var result = await service.CreateAsync(snapshot, new SupportBundleOptions { OutputDirectory = _tempRoot, IncludeCameraSerial = true });

        using var archive = ZipFile.OpenRead(result.BundlePath);
        using var reader = new StreamReader(archive.GetEntry("diagnostics.txt")!.Open());
        var text = await reader.ReadToEndAsync();
        Assert.Contains("SERIAL-INCLUDED", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupportBundleService_ReportsProgress()
    {
        var service = CreateSupportBundleService(out var diagnostics, out _, out _);
        var stages = new List<string>();

        await service.CreateAsync(
            diagnostics.CreateSnapshot(new DiagnosticsState()),
            new SupportBundleOptions { OutputDirectory = _tempRoot },
            new Progress<SupportBundleProgress>(progress => stages.Add(progress.Stage)));

        Assert.Contains("Gathering diagnostics", stages);
        Assert.Contains("Complete", stages);
    }

    [Fact]
    public async Task SupportBundleService_CancellationCleansTemporaryFolder()
    {
        var service = CreateSupportBundleService(out var diagnostics, out _, out _);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await service.CreateAsync(
            diagnostics.CreateSnapshot(new DiagnosticsState()),
            new SupportBundleOptions { OutputDirectory = _tempRoot },
            cancellationToken: cancellation.Token);

        Assert.True(result.WasCanceled);
        var tempRoot = Path.Combine(Path.GetTempPath(), "dcam-support-bundle");
        if (Directory.Exists(tempRoot))
        {
            Assert.DoesNotContain(Directory.EnumerateDirectories(tempRoot), directory => directory.Contains(result.BundleId.ToString("N"), StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SupportBundleService_HandlesMissingLogDirectory()
    {
        var diagnostics = new ApplicationDiagnosticsService(new InMemoryDiagnosticLogStore());
        var service = new SupportBundleService(diagnostics, new InMemoryDiagnosticLogStore(), Path.Combine(_tempRoot, "missing-logs"));

        var result = await service.CreateAsync(diagnostics.CreateSnapshot(new DiagnosticsState()), new SupportBundleOptions { OutputDirectory = _tempRoot });

        Assert.True(File.Exists(result.BundlePath));
    }

    [Fact]
    public async Task SupportBundleService_ManifestIncludesSchemaVersion()
    {
        var service = CreateSupportBundleService(out var diagnostics, out _, out _);

        var result = await service.CreateAsync(diagnostics.CreateSnapshot(new DiagnosticsState()), new SupportBundleOptions { OutputDirectory = _tempRoot });

        using var archive = ZipFile.OpenRead(result.BundlePath);
        using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
        var manifest = await reader.ReadToEndAsync();
        Assert.Contains("\"schemaVersion\": \"1.0\"", manifest, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private DiagnosticsLoggerProvider CreateProvider(out InMemoryDiagnosticLogStore store, out string logDirectory)
    {
        logDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "logs", Guid.NewGuid().ToString("N"))).FullName;
        store = new InMemoryDiagnosticLogStore();
        return new DiagnosticsLoggerProvider(store, new DiagnosticsLoggingOptions { LogDirectory = logDirectory });
    }

    private SupportBundleService CreateSupportBundleService(
        out ApplicationDiagnosticsService diagnostics,
        out InMemoryDiagnosticLogStore store,
        out string logDirectory)
    {
        logDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "logs", Guid.NewGuid().ToString("N"))).FullName;
        store = new InMemoryDiagnosticLogStore();
        diagnostics = new ApplicationDiagnosticsService(store);
        return new SupportBundleService(diagnostics, store, logDirectory);
    }

    private static DiagnosticLogEntry CreateEntry(
        string message,
        LogLevel level = LogLevel.Information,
        string category = "Test",
        IReadOnlyDictionary<string, string>? properties = null)
    {
        return new DiagnosticLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Properties = properties ?? new Dictionary<string, string>()
        };
    }
}
