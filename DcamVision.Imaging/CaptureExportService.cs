using Microsoft.Extensions.Logging;

namespace DcamVision.Imaging;

public sealed class CaptureExportService : ICaptureExportService
{
    private readonly Tiff16CaptureWriter _tiffWriter = new();
    private readonly RawMono16CaptureWriter _rawWriter = new();
    private readonly JsonMetadataCaptureWriter _jsonWriter = new();
    private readonly ExportManifestWriter _manifestWriter = new();
    private readonly PngPreviewCaptureWriter _pngWriter;
    private readonly ILogger<CaptureExportService>? _logger;
    private int _isExporting;

    public CaptureExportService(ImageDisplayProcessor displayProcessor, ILogger<CaptureExportService>? logger = null)
    {
        _pngWriter = new PngPreviewCaptureWriter(displayProcessor);
        _logger = logger;
    }

    public async Task<CaptureExportResult> ExportAsync(
        CaptureExportRequest request,
        IProgress<CaptureExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CaptureExportValidator.Validate(request);
        if (Interlocked.Exchange(ref _isExporting, 1) == 1)
        {
            throw new InvalidOperationException("An export is already running.");
        }

        var exportId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var items = new List<CaptureExportItemResult>();
        var resolver = new ExportPathResolver();
        var sessionLookup = request.Sessions.ToDictionary(session => session.SessionId);
        var formats = request.Options.EnabledFormats();
        var canceled = false;

        _logger?.LogInformation("Export started. Export ID: {ExportId}. Capture count: {Count}. Destination: {Destination}.", exportId, request.Captures.Count, request.OutputDirectory);

        try
        {
            Directory.CreateDirectory(request.OutputDirectory);

            for (var i = 0; i < request.Captures.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    canceled = true;
                    break;
                }

                var capture = request.Captures[i];
                sessionLookup.TryGetValue(capture.SessionId, out var session);
                var itemFiles = new List<CaptureExportFileResult>();

                try
                {
                    var directory = ResolveDirectory(request.OutputDirectory, request.Options.DirectoryLayout, session);
                    var baseName = ExportFilenameTemplate.Expand(request.Options.FilenameTemplate, capture, session, i + 1);

                    foreach (var format in formats)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(new CaptureExportProgress
                        {
                            CompletedCaptures = i,
                            TotalCaptures = request.Captures.Count,
                            CurrentCaptureName = baseName,
                            CurrentFormat = format
                        });

                        var path = resolver.Resolve(directory, baseName, ExtensionFor(format), request.Options.CollisionBehavior);
                        if (path.Length == 0)
                        {
                            continue;
                        }

                        itemFiles.Add(await WriteFormatAsync(path, format, capture, session, request, cancellationToken).ConfigureAwait(false));
                    }

                    var skipped = itemFiles.Count == 0;
                    items.Add(new CaptureExportItemResult
                    {
                        CaptureId = capture.CaptureId,
                        Success = !skipped,
                        Skipped = skipped,
                        ErrorMessage = skipped ? "All output files were skipped due to collisions." : null,
                        Files = itemFiles
                    });
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                    break;
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Capture export failed for {CaptureId}.", capture.CaptureId);
                    items.Add(new CaptureExportItemResult
                    {
                        CaptureId = capture.CaptureId,
                        Success = false,
                        ErrorMessage = exception.Message
                    });
                }

                progress?.Report(new CaptureExportProgress
                {
                    CompletedCaptures = i + 1,
                    TotalCaptures = request.Captures.Count
                });
            }

            if (request.Options.CreateManifest && !canceled)
            {
                var manifestPath = Path.Combine(request.OutputDirectory, "manifest.json");
                await _manifestWriter.WriteAsync(manifestPath, exportId, startedAt, request, items, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isExporting, 0);
        }

        var result = new CaptureExportResult
        {
            ExportId = exportId,
            RequestedCaptureCount = request.Captures.Count,
            ExportedCaptureCount = items.Count(item => item.Success),
            SkippedCaptureCount = items.Count(item => item.Skipped),
            WasCanceled = canceled,
            OutputDirectory = request.OutputDirectory,
            Items = items
        };

        _logger?.LogInformation("Export completed. Export ID: {ExportId}. {Summary}", exportId, result.Summary);
        return result;
    }

    private async Task<CaptureExportFileResult> WriteFormatAsync(
        string path,
        CaptureExportFormat format,
        CaptureRecord capture,
        CaptureSession? session,
        CaptureExportRequest request,
        CancellationToken cancellationToken)
    {
        return format switch
        {
            CaptureExportFormat.Tiff16 => await AtomicFileWriter.WriteAsync(path, format, (stream, token) => _tiffWriter.WriteAsync(capture, stream, token), request.OutputDirectory, cancellationToken).ConfigureAwait(false),
            CaptureExportFormat.PngPreview => await AtomicFileWriter.WriteAsync(path, format, (stream, token) => _pngWriter.WriteAsync(capture, request.Options.PreviewDisplaySettings, stream, token), request.OutputDirectory, cancellationToken).ConfigureAwait(false),
            CaptureExportFormat.RawMono16 => await AtomicFileWriter.WriteAsync(path, format, (stream, token) => _rawWriter.WriteAsync(capture, stream, token), request.OutputDirectory, cancellationToken).ConfigureAwait(false),
            CaptureExportFormat.JsonMetadata => await AtomicFileWriter.WriteAsync(path, format, (stream, token) => _jsonWriter.WriteAsync(capture, session, stream, token), request.OutputDirectory, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported export format: {format}.")
        };
    }

    private static string ResolveDirectory(string root, ExportDirectoryLayout layout, CaptureSession? session)
    {
        return layout == ExportDirectoryLayout.BySession
            ? Path.Combine(root, ExportFilenameSanitizer.SanitizeComponent(session?.Name ?? "Session"))
            : root;
    }

    private static string ExtensionFor(CaptureExportFormat format)
    {
        return format switch
        {
            CaptureExportFormat.Tiff16 => ".tif",
            CaptureExportFormat.PngPreview => ".png",
            CaptureExportFormat.RawMono16 => ".raw",
            CaptureExportFormat.JsonMetadata => ".json",
            _ => ".dat"
        };
    }
}
