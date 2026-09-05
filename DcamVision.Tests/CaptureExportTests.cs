using System.Text.Json;
using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.Tests;

public sealed class CaptureExportTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "dcam-export-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void CaptureExportValidator_RejectsEmptyCaptures()
    {
        var request = CreateRequest([]);

        Assert.Throws<ArgumentException>(() => CaptureExportValidator.Validate(request));
    }

    [Fact]
    public void CaptureExportValidator_RejectsNoFormats()
    {
        var request = CreateRequest([CreateRecord()], options: new CaptureExportOptions
        {
            ExportTiff16 = false,
            ExportPngPreview = false,
            ExportRawMono16 = false,
            ExportJsonMetadata = false
        });

        Assert.Throws<ArgumentException>(() => CaptureExportValidator.Validate(request));
    }

    [Fact]
    public void ExportFilenameTemplate_ExpandsTokensDeterministically()
    {
        var session = CreateSession("Session A");
        var capture = CreateRecord(session.SessionId);

        var name = ExportFilenameTemplate.Expand("{session}_{captureIndex}_{source}_{frameNumber}_{cameraModel}", capture, session, 7);

        Assert.Equal("Session_A_0007_Manual_42_ORCA-Sim", name);
    }

    [Fact]
    public void ExportFilenameSanitizer_RemovesInvalidWindowsCharacters()
    {
        var name = ExportFilenameSanitizer.SanitizeComponent(" bad:name*with?chars. ");

        Assert.Equal("bad_name_with_chars", name);
    }

    [Fact]
    public void ExportPathResolver_RenamesCollisions()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "image.tif"), "existing");
        var resolver = new ExportPathResolver();

        var path = resolver.Resolve(_tempDirectory, "image", ".tif", ExportCollisionBehavior.Rename);

        Assert.EndsWith("image_001.tif", path);
    }

    [Fact]
    public void ExportPathResolver_SkipsCollisions()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "image.tif"), "existing");
        var resolver = new ExportPathResolver();

        var path = resolver.Resolve(_tempDirectory, "image", ".tif", ExportCollisionBehavior.Skip);

        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void ExportPathResolver_OverwriteReturnsOriginalPath()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "image.tif"), "existing");
        var resolver = new ExportPathResolver();

        var path = resolver.Resolve(_tempDirectory, "image", ".tif", ExportCollisionBehavior.Overwrite);

        Assert.Equal(Path.Combine(_tempDirectory, "image.tif"), path);
    }

    [Fact]
    public async Task RawMono16CaptureWriter_WritesLittleEndianRowMajorPixels()
    {
        await using var stream = new MemoryStream();
        var writer = new RawMono16CaptureWriter();

        await writer.WriteAsync(CreateRecord(pixels: [0x1234, 0xABCD]), stream);

        Assert.Equal([0x34, 0x12, 0xCD, 0xAB], stream.ToArray());
    }

    [Fact]
    public async Task RawMono16CaptureWriter_UsesRawPixelsDespiteDisplaySettings()
    {
        await using var stream = new MemoryStream();
        var writer = new RawMono16CaptureWriter();
        var record = CreateRecord(pixels: [100, 200]);

        _ = new ImageDisplayProcessor().Process(record.Frame, ImageDisplaySettings.Default with { BlackPoint = 100, WhitePoint = 200, Invert = true });
        await writer.WriteAsync(record, stream);

        Assert.Equal([100, 0, 200, 0], stream.ToArray());
    }

    [Fact]
    public async Task Tiff16CaptureWriter_PreservesRawPixelValues()
    {
        await using var stream = new MemoryStream();
        var writer = new Tiff16CaptureWriter();

        await writer.WriteAsync(CreateRecord(pixels: [0x0001, 0x0203]), stream);
        var bytes = stream.ToArray();

        Assert.Equal((byte)'I', bytes[0]);
        Assert.Equal(1, BitConverter.ToUInt16(bytes, bytes.Length - 4));
        Assert.Equal(0x0203, BitConverter.ToUInt16(bytes, bytes.Length - 2));
    }

    [Fact]
    public async Task PngPreviewCaptureWriter_WritesPngSignature()
    {
        await using var stream = new MemoryStream();
        var writer = new PngPreviewCaptureWriter(new ImageDisplayProcessor());

        await writer.WriteAsync(CreateRecord(), ImageDisplaySettings.Default, stream);

        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], stream.ToArray()[..8]);
    }

    [Fact]
    public async Task PngPreviewCaptureWriter_UsesDisplaySettings()
    {
        var writer = new PngPreviewCaptureWriter(new ImageDisplayProcessor());
        await using var linear = new MemoryStream();
        await using var gamma = new MemoryStream();
        var record = CreateRecord(pixels: [0, 25, 50, 100]);

        await writer.WriteAsync(record, ImageDisplaySettings.Default with { BlackPoint = 0, WhitePoint = 100, Gamma = 1 }, linear);
        await writer.WriteAsync(record, ImageDisplaySettings.Default with { BlackPoint = 0, WhitePoint = 100, Gamma = 2 }, gamma);

        Assert.NotEqual(linear.ToArray(), gamma.ToArray());
    }

    [Fact]
    public async Task JsonMetadataCaptureWriter_WritesSchemaVersionAndMetadata()
    {
        await using var stream = new MemoryStream();
        var writer = new JsonMetadataCaptureWriter();
        var session = CreateSession("Session A");

        await writer.WriteAsync(CreateRecord(session.SessionId, notes: "note", tags: ["test"]), session, stream);
        var document = JsonDocument.Parse(stream.ToArray());

        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("Manual", document.RootElement.GetProperty("source").GetString());
        Assert.Equal(25, document.RootElement.GetProperty("exposure").GetProperty("milliseconds").GetDouble());
        Assert.Equal("note", document.RootElement.GetProperty("notes").GetString());
        Assert.Equal("test", document.RootElement.GetProperty("tags")[0].GetString());
        Assert.True(document.RootElement.GetProperty("propertySnapshot").TryGetProperty("sensor.gain", out _));
    }

    [Fact]
    public void CaptureExportSizeEstimator_EstimatesEnabledFormats()
    {
        var request = CreateRequest([CreateRecord()], options: new CaptureExportOptions { ExportRawMono16 = true });

        var estimate = CaptureExportSizeEstimator.EstimateBytes(request);

        Assert.True(estimate > 8);
    }

    [Fact]
    public async Task CaptureExportService_ExportsSingleCaptureFiles()
    {
        var session = CreateSession("Session A");
        var record = CreateRecord(session.SessionId);
        var service = CreateService();

        var result = await service.ExportAsync(CreateRequest([record], [session]));

        Assert.True(result.Items.Single().Success);
        Assert.Contains(result.Items.Single().Files, file => file.Format == CaptureExportFormat.Tiff16);
        Assert.Contains(result.Items.Single().Files, file => file.Format == CaptureExportFormat.PngPreview);
        Assert.Contains(result.Items.Single().Files, file => file.Format == CaptureExportFormat.JsonMetadata);
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "manifest.json")));
    }

    [Fact]
    public async Task CaptureExportService_ExportsRawWhenEnabled()
    {
        var service = CreateService();
        var record = CreateRecord(pixels: [1, 2, 3, 4]);

        var result = await service.ExportAsync(CreateRequest([record], options: new CaptureExportOptions
        {
            ExportTiff16 = false,
            ExportPngPreview = false,
            ExportRawMono16 = true,
            ExportJsonMetadata = false,
            CreateManifest = false
        }));

        var file = result.Items.Single().Files.Single();
        Assert.Equal(8, new FileInfo(Path.Combine(_tempDirectory, file.RelativePath)).Length);
    }

    [Fact]
    public async Task CaptureExportService_UsesDirectoryBySessionLayout()
    {
        var session = CreateSession("Dark Current");
        var service = CreateService();

        var result = await service.ExportAsync(CreateRequest([CreateRecord(session.SessionId)], [session]));

        Assert.All(result.Items.Single().Files, file => Assert.StartsWith("Dark_Current", file.RelativePath));
    }

    [Fact]
    public async Task CaptureExportService_RenamesCollidingFiles()
    {
        var service = CreateService();
        var options = new CaptureExportOptions
        {
            ExportPngPreview = false,
            ExportJsonMetadata = false,
            CreateManifest = false,
            FilenameTemplate = "same",
            CollisionBehavior = ExportCollisionBehavior.Rename,
            DirectoryLayout = ExportDirectoryLayout.Flat
        };

        var result = await service.ExportAsync(CreateRequest([CreateRecord(), CreateRecord()], options: options));

        Assert.Contains(result.Items.SelectMany(item => item.Files), file => file.RelativePath == "same_001.tif");
    }

    [Fact]
    public async Task CaptureExportService_SkipsCollidingFiles()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "same.tif"), "existing");
        var service = CreateService();
        var options = new CaptureExportOptions
        {
            ExportPngPreview = false,
            ExportJsonMetadata = false,
            CreateManifest = false,
            FilenameTemplate = "same",
            CollisionBehavior = ExportCollisionBehavior.Skip,
            DirectoryLayout = ExportDirectoryLayout.Flat
        };

        var result = await service.ExportAsync(CreateRequest([CreateRecord()], options: options));

        Assert.Equal(1, result.SkippedCaptureCount);
    }

    [Fact]
    public async Task CaptureExportService_OverwritesCollidingFiles()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "same.tif");
        File.WriteAllText(path, "existing");
        var service = CreateService();
        var options = new CaptureExportOptions
        {
            ExportPngPreview = false,
            ExportJsonMetadata = false,
            CreateManifest = false,
            FilenameTemplate = "same",
            CollisionBehavior = ExportCollisionBehavior.Overwrite,
            DirectoryLayout = ExportDirectoryLayout.Flat
        };

        await service.ExportAsync(CreateRequest([CreateRecord()], options: options));

        Assert.True(new FileInfo(path).Length > "existing".Length);
    }

    [Fact]
    public async Task CaptureExportService_ReportsProgress()
    {
        var service = CreateService();
        var progress = new List<CaptureExportProgress>();

        await service.ExportAsync(CreateRequest([CreateRecord(), CreateRecord()]), new Progress<CaptureExportProgress>(progress.Add));

        Assert.Contains(progress, item => item.CompletedCaptures == 2);
    }

    [Fact]
    public async Task CaptureExportService_CancellationReturnsCanceledResult()
    {
        var service = CreateService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await service.ExportAsync(CreateRequest([CreateRecord(), CreateRecord()]), cancellationToken: cancellation.Token);

        Assert.True(result.WasCanceled);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task AtomicFileWriter_CleansUpTempFileOnCancellation()
    {
        var path = Path.Combine(_tempDirectory, "file.dat");
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(() => AtomicFileWriter.WriteAsync(
            path,
            CaptureExportFormat.RawMono16,
            async (stream, token) =>
            {
                await stream.WriteAsync(new byte[] { 1, 2, 3 }, token);
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
            },
            _tempDirectory,
            cancellation.Token));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task CaptureExportService_ContinuesAfterPerCaptureFailure()
    {
        var valid = CreateRecord();
        var invalid = valid with { CaptureId = Guid.NewGuid(), Frame = valid.Frame with { Pixels = [1] } };
        var service = CreateService();

        var result = await service.ExportAsync(CreateRequest([invalid, valid], options: new CaptureExportOptions
        {
            ExportPngPreview = false,
            ExportJsonMetadata = false,
            CreateManifest = false
        }));

        Assert.Contains(result.Items, item => !item.Success);
        Assert.Contains(result.Items, item => item.Success);
    }

    [Fact]
    public async Task CaptureExportService_WritesManifestForBatchExport()
    {
        var service = CreateService();

        var result = await service.ExportAsync(CreateRequest([CreateRecord(), CreateRecord()]));
        var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(_tempDirectory, "manifest.json")));

        Assert.Equal("1.0", manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(result.ExportId, manifest.RootElement.GetProperty("exportId").GetGuid());
        Assert.Equal(2, manifest.RootElement.GetProperty("captureCount").GetInt32());
    }

    [Fact]
    public async Task CaptureExportService_AddsSha256Checksums()
    {
        var service = CreateService();

        var result = await service.ExportAsync(CreateRequest([CreateRecord()]));

        Assert.All(result.Items.Single().Files, file => Assert.False(string.IsNullOrWhiteSpace(file.Sha256)));
    }

    [Fact]
    public async Task CaptureExportService_PreventsConcurrentExports()
    {
        var service = CreateService();
        var request = CreateRequest(Enumerable.Range(0, 20).Select(_ => CreateRecord()).ToArray(), options: new CaptureExportOptions { ExportPngPreview = false, ExportJsonMetadata = false, CreateManifest = false });

        var running = service.ExportAsync(request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(request));
        await running;
    }

    [Fact]
    public async Task CaptureExportService_ExportsSessionSubset()
    {
        var sessionA = CreateSession("A");
        var sessionB = CreateSession("B");
        var captures = new[] { CreateRecord(sessionA.SessionId), CreateRecord(sessionB.SessionId), CreateRecord(sessionA.SessionId) };
        var service = CreateService();

        var request = CreateRequest(captures.Where(capture => capture.SessionId == sessionA.SessionId).ToArray(), [sessionA, sessionB]);
        var result = await service.ExportAsync(request);

        Assert.Equal(2, result.ExportedCaptureCount);
    }

    private CaptureExportService CreateService()
    {
        return new CaptureExportService(new ImageDisplayProcessor());
    }

    private CaptureExportRequest CreateRequest(
        IReadOnlyList<CaptureRecord> captures,
        IReadOnlyList<CaptureSession>? sessions = null,
        CaptureExportOptions? options = null)
    {
        return new CaptureExportRequest
        {
            Captures = captures,
            Sessions = sessions ?? [CreateSession("Session A")],
            OutputDirectory = _tempDirectory,
            Options = options ?? new CaptureExportOptions
            {
                FilenameTemplate = "{session}_{captureIndex}",
                DirectoryLayout = ExportDirectoryLayout.Flat
            }
        };
    }

    private static CaptureSession CreateSession(string name)
    {
        return new CaptureSession
        {
            SessionId = Guid.NewGuid(),
            StartedAt = DateTimeOffset.Parse("2026-09-05T10:42:00Z"),
            Name = name
        };
    }

    private static CaptureRecord CreateRecord(
        Guid? sessionId = null,
        ushort[]? pixels = null,
        string notes = "",
        IReadOnlyList<string>? tags = null)
    {
        var framePixels = pixels ?? [0, 1000, 32000, 65535];
        var frame = new CameraFrame
        {
            Width = framePixels.Length,
            Height = 1,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = framePixels,
            Timestamp = DateTimeOffset.Parse("2026-09-05T10:42:31Z"),
            FrameNumber = 42,
            Exposure = TimeSpan.FromMilliseconds(25)
        };

        return new CaptureRecord
        {
            CaptureId = Guid.NewGuid(),
            SessionId = sessionId ?? Guid.NewGuid(),
            SequenceNumber = 1,
            Frame = frame,
            CapturedAt = frame.Timestamp,
            Source = CaptureSource.Manual,
            Metadata = new CaptureMetadata
            {
                CameraId = "simulated-hamamatsu-orca",
                CameraDisplayName = "Simulated Hamamatsu ORCA",
                Manufacturer = "Hamamatsu",
                Model = "ORCA-Sim",
                SerialNumber = "SIM-0001",
                Exposure = frame.Exposure,
                Gain = 1.5,
                Width = frame.Width,
                Height = frame.Height,
                PixelFormat = frame.PixelFormat,
                TriggerMode = "Internal",
                PropertySnapshot = new Dictionary<string, object?>
                {
                    ["exposure.time"] = 25.0,
                    ["sensor.gain"] = 1.5,
                    ["image.width"] = frame.Width,
                    ["image.height"] = frame.Height,
                    ["image.pixelFormat"] = "Mono16",
                    ["trigger.mode"] = "Internal",
                    ["image.binning"] = 1,
                    ["sensor.readoutSpeed"] = "Normal",
                    ["sensor.coolingEnabled"] = true
                }
            },
            Statistics = FrameStatisticsCalculator.Calculate(frame),
            Notes = notes,
            Tags = tags ?? []
        };
    }
}
