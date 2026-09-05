using System.Text.Json;

namespace DcamVision.Imaging;

public sealed class ExportManifestWriter
{
    public const string SchemaVersion = "1.0";

    public async Task WriteAsync(
        string path,
        Guid exportId,
        DateTimeOffset createdAt,
        CaptureExportRequest request,
        IReadOnlyList<CaptureExportItemResult> items,
        CancellationToken cancellationToken = default)
    {
        var dto = new ManifestDto(
            SchemaVersion,
            exportId,
            createdAt,
            "DCAM Vision Studio",
            request.Captures.Count,
            request.Options.EnabledFormats().Select(format => format.ToString()).ToArray(),
            request.Sessions.Select(session => new ManifestSessionDto(session.SessionId, session.Name)).ToArray(),
            items.Select(item => new ManifestItemDto(
                item.CaptureId,
                item.Success,
                item.Skipped,
                item.ErrorMessage,
                item.Files.Select(file => new ManifestFileDto(file.Format.ToString(), file.RelativePath, file.BytesWritten, file.Sha256)).ToArray())).ToArray());

        await AtomicFileWriter.WriteAsync(
            path,
            CaptureExportFormat.JsonMetadata,
            async (stream, token) => await JsonSerializer.SerializeAsync(stream, dto, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }, token).ConfigureAwait(false),
            Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory(),
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record ManifestDto(
        string SchemaVersion,
        Guid ExportId,
        DateTimeOffset CreatedAt,
        string Application,
        int CaptureCount,
        IReadOnlyList<string> ExportedFormats,
        IReadOnlyList<ManifestSessionDto> Sessions,
        IReadOnlyList<ManifestItemDto> Items);

    private sealed record ManifestSessionDto(Guid SessionId, string Name);

    private sealed record ManifestItemDto(
        Guid CaptureId,
        bool Success,
        bool Skipped,
        string? ErrorMessage,
        IReadOnlyList<ManifestFileDto> Files);

    private sealed record ManifestFileDto(
        string Format,
        string RelativePath,
        long BytesWritten,
        string? Sha256);
}
