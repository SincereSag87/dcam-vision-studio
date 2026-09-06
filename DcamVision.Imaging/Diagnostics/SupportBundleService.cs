using System.IO.Compression;
using System.Text.Json;

namespace DcamVision.Imaging.Diagnostics;

public sealed class SupportBundleService
{
    private readonly IApplicationDiagnosticsService _diagnosticsService;
    private readonly InMemoryDiagnosticLogStore _logStore;
    private readonly string _logDirectory;

    public SupportBundleService(
        IApplicationDiagnosticsService diagnosticsService,
        InMemoryDiagnosticLogStore logStore,
        string? logDirectory = null)
    {
        _diagnosticsService = diagnosticsService;
        _logStore = logStore;
        _logDirectory = logDirectory ?? DiagnosticsPaths.DefaultLogDirectory();
    }

    public async Task<SupportBundleResult> CreateAsync(
        ApplicationDiagnosticsSnapshot snapshot,
        SupportBundleOptions? options = null,
        IProgress<SupportBundleProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SupportBundleOptions();
        var bundleId = Guid.NewGuid();
        var outputDirectory = options.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);
        var tempRoot = Path.Combine(Path.GetTempPath(), "dcam-support-bundle", bundleId.ToString("N"));
        var finalPath = Path.Combine(outputDirectory, $"dcam-vision-studio-support-{bundleId:N}.zip");
        var includedFiles = new List<string>();

        try
        {
            progress?.Report(new SupportBundleProgress("Gathering diagnostics", 10));
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(tempRoot);

            var redactedSnapshot = RedactSnapshot(snapshot, options);
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "diagnostics.json"),
                JsonSerializer.Serialize(redactedSnapshot, jsonOptions),
                cancellationToken).ConfigureAwait(false);
            includedFiles.Add("diagnostics.json");

            progress?.Report(new SupportBundleProgress("Creating text report", 30));
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "diagnostics.txt"),
                _diagnosticsService.CreateTextReport(redactedSnapshot, options),
                cancellationToken).ConfigureAwait(false);
            includedFiles.Add("diagnostics.txt");

            progress?.Report(new SupportBundleProgress("Collecting recent errors", 45));
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "recent-errors.json"),
                JsonSerializer.Serialize(_logStore.RecentErrors, jsonOptions),
                cancellationToken).ConfigureAwait(false);
            includedFiles.Add("recent-errors.json");

            progress?.Report(new SupportBundleProgress("Collecting logs", 60));
            var logsDirectory = Path.Combine(tempRoot, "logs");
            Directory.CreateDirectory(logsDirectory);
            if (Directory.Exists(_logDirectory))
            {
                foreach (var logFile in Directory.EnumerateFiles(_logDirectory, "dcam-vision-studio-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(options.IncludeDebugLogs ? 7 : 3))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destination = Path.Combine(logsDirectory, Path.GetFileName(logFile));
                    File.Copy(logFile, destination, overwrite: true);
                    includedFiles.Add($"logs/{Path.GetFileName(logFile)}");
                }
            }

            progress?.Report(new SupportBundleProgress("Writing manifest", 75));
            var manifest = new
            {
                schemaVersion = "1.0",
                bundleId,
                generatedAt = DateTimeOffset.UtcNow,
                applicationVersion = snapshot.Application.ApplicationVersion,
                redaction = new
                {
                    options.IncludeCameraSerial,
                    options.IncludeFullPaths,
                    options.IncludeDebugLogs
                },
                includedFiles
            };
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "README.txt"),
                "DCAM Vision Studio support bundle. Image captures and exported scientific data are not included by default.",
                cancellationToken).ConfigureAwait(false);
            includedFiles.Add("README.txt");
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest, jsonOptions),
                cancellationToken).ConfigureAwait(false);
            includedFiles.Add("manifest.json");

            progress?.Report(new SupportBundleProgress("Creating ZIP", 90));
            cancellationToken.ThrowIfCancellationRequested();
            var tempZip = finalPath + ".tmp";
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            ZipFile.CreateFromDirectory(tempRoot, tempZip);
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempZip, finalPath);
            progress?.Report(new SupportBundleProgress("Complete", 100));
            return new SupportBundleResult
            {
                BundleId = bundleId,
                BundlePath = finalPath,
                WasCanceled = false,
                IncludedFiles = includedFiles
            };
        }
        catch (OperationCanceledException)
        {
            var tempZip = finalPath + ".tmp";
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            return new SupportBundleResult
            {
                BundleId = bundleId,
                BundlePath = finalPath,
                WasCanceled = true,
                IncludedFiles = includedFiles
            };
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static ApplicationDiagnosticsSnapshot RedactSnapshot(ApplicationDiagnosticsSnapshot snapshot, SupportBundleOptions options)
    {
        return snapshot with
        {
            Camera = snapshot.Camera with
            {
                CameraId = options.IncludeCameraSerial ? snapshot.Camera.CameraId : "redacted",
                DcamUnavailableReason = snapshot.Camera.DcamUnavailableReason is null ? null : Redactor.Redact(snapshot.Camera.DcamUnavailableReason, options.IncludeFullPaths)
            },
            Export = snapshot.Export with
            {
                LastExportDestination = snapshot.Export.LastExportDestination is null ? null : Redactor.Redact(snapshot.Export.LastExportDestination, options.IncludeFullPaths)
            },
            RecentErrors = snapshot.RecentErrors.Select(entry => entry with
            {
                Message = Redactor.Redact(entry.Message, options.IncludeFullPaths),
                ExceptionMessage = entry.ExceptionMessage is null ? null : Redactor.Redact(entry.ExceptionMessage, options.IncludeFullPaths),
                StackTrace = null
            }).ToArray()
        };
    }
}
