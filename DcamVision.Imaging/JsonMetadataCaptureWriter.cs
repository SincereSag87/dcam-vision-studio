using System.Text.Json;
using System.Text.Json.Serialization;
using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed class JsonMetadataCaptureWriter
{
    public const string SchemaVersion = "1.0";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(
        CaptureRecord capture,
        CaptureSession? session,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var histogram = HistogramAnalyzer.Analyze(capture.Frame);
        var dto = new CaptureMetadataDto(
            SchemaVersion,
            capture.CaptureId,
            capture.SessionId,
            session?.Name,
            capture.Source.ToString(),
            capture.CapturedAt,
            new CameraDto(
                capture.Metadata.CameraId,
                capture.Metadata.CameraDisplayName,
                capture.Metadata.Manufacturer,
                capture.Metadata.Model,
                capture.Metadata.SerialNumber),
            new ExposureDto(
                capture.Metadata.Exposure.TotalSeconds,
                capture.Metadata.Exposure.TotalMilliseconds),
            capture.Metadata.Gain,
            capture.Metadata.Width,
            capture.Metadata.Height,
            capture.Metadata.PixelFormat.ToString(),
            capture.Metadata.TriggerMode,
            new StatisticsDto(
                capture.Statistics.Minimum,
                capture.Statistics.Maximum,
                capture.Statistics.Mean,
                histogram.Median,
                histogram.StandardDeviation,
                capture.Statistics.SaturatedPixelCount,
                capture.Statistics.SaturationPercentage),
            capture.Notes,
            capture.Tags,
            capture.Metadata.PropertySnapshot.ToDictionary(pair => pair.Key, pair => ToJsonPrimitive(pair.Value), StringComparer.OrdinalIgnoreCase),
            new RawLayoutDto("row-major", "little-endian", 16, "ushort"));

        await JsonSerializer.SerializeAsync(stream, dto, Options, cancellationToken).ConfigureAwait(false);
    }

    private static object? ToJsonPrimitive(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or int or long or double or float or decimal => value,
            TimeSpan timeSpan => new ExposureDto(timeSpan.TotalSeconds, timeSpan.TotalMilliseconds),
            CameraPixelFormat pixelFormat => pixelFormat.ToString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private sealed record CaptureMetadataDto(
        string SchemaVersion,
        Guid CaptureId,
        Guid SessionId,
        string? SessionName,
        string Source,
        DateTimeOffset CapturedAt,
        CameraDto Camera,
        ExposureDto Exposure,
        double Gain,
        int Width,
        int Height,
        string PixelFormat,
        string TriggerMode,
        StatisticsDto Statistics,
        string Notes,
        IReadOnlyList<string> Tags,
        IReadOnlyDictionary<string, object?> PropertySnapshot,
        RawLayoutDto RawMono16Layout);

    private sealed record CameraDto(
        string CameraId,
        string DisplayName,
        string Manufacturer,
        string Model,
        string SerialNumber);

    private sealed record ExposureDto(double Seconds, double Milliseconds);

    private sealed record StatisticsDto(
        ushort Minimum,
        ushort Maximum,
        double Mean,
        double Median,
        double StandardDeviation,
        int SaturatedPixelCount,
        double SaturationPercentage);

    private sealed record RawLayoutDto(
        string Layout,
        string ByteOrder,
        int BitDepth,
        string PixelType);
}
