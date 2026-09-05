using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.Tests;

public sealed class CaptureHistoryTests
{
    [Fact]
    public async Task CaptureRecordFactory_CreatesStableIdsAndMetadataSnapshot()
    {
        var camera = await CreateCameraAsync();
        var factory = new CaptureRecordFactory(camera);
        var sessionId = Guid.NewGuid();

        var record = await factory.CreateAsync(sessionId, 0, CreateFrame([1, 2, 3, 4]), CaptureSource.Manual);

        Assert.NotEqual(Guid.Empty, record.CaptureId);
        Assert.Equal(sessionId, record.SessionId);
        Assert.Equal("Simulated Hamamatsu ORCA", record.Metadata.CameraDisplayName);
        Assert.True(record.Metadata.PropertySnapshot.ContainsKey("exposure.time"));
    }

    [Fact]
    public async Task CaptureRecordFactory_MetadataDoesNotChangeAfterCameraSettingsChange()
    {
        var camera = await CreateCameraAsync();
        var factory = new CaptureRecordFactory(camera);
        await camera.SetExposureAsync(TimeSpan.FromMilliseconds(10));
        var record = await factory.CreateAsync(Guid.NewGuid(), 0, CreateFrame([1, 2, 3, 4], TimeSpan.FromMilliseconds(10)), CaptureSource.Manual);

        await camera.SetExposureAsync(TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(10), record.Metadata.Exposure);
        Assert.Equal(10.0, record.Metadata.PropertySnapshot["exposure.time"]);
    }

    [Fact]
    public async Task CaptureHistoryStore_ClonesPixelBufferOnInsert()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var pixels = new ushort[] { 1, 2, 3, 4 };
        var record = await CreateRecordAsync(session.SessionId, pixels: pixels);

        var stored = store.AddCapture(record);
        pixels[0] = 999;

        Assert.Equal(1, stored.Frame.Pixels[0]);
        Assert.Equal(1, store.Captures.Single().Frame.Pixels[0]);
    }

    [Fact]
    public void CaptureHistoryStore_FirstCaptureAutoSessionCanBeEnsured()
    {
        var store = new CaptureHistoryStore();

        var session = store.EnsureActiveSession();

        Assert.NotNull(store.ActiveSession);
        Assert.Equal(session.SessionId, store.ActiveSession.SessionId);
    }

    [Fact]
    public void CaptureHistoryStore_NewSessionChangesActiveSession()
    {
        var store = new CaptureHistoryStore();
        var first = store.StartNewSession("First");

        var second = store.StartNewSession("Second");

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal("Second", store.ActiveSession!.Name);
    }

    [Fact]
    public void CaptureHistoryStore_RenameSessionTrimsName()
    {
        var store = new CaptureHistoryStore();
        var session = store.StartNewSession("Original");

        var renamed = store.RenameSession(session.SessionId, "  Dark Current  ");

        Assert.Equal("Dark Current", renamed.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CaptureHistoryStore_RenameSessionRejectsEmptyNames(string name)
    {
        var store = new CaptureHistoryStore();
        var session = store.StartNewSession("Original");

        Assert.Throws<ArgumentException>(() => store.RenameSession(session.SessionId, name));
    }

    [Fact]
    public async Task CaptureHistoryStore_EnforcesRetentionLimitByEvictingOldest()
    {
        var store = new CaptureHistoryStore(new CaptureHistoryOptions { MaximumCaptures = 2 });
        var session = store.EnsureActiveSession();

        var first = store.AddCapture(await CreateRecordAsync(session.SessionId, sequence: 1));
        store.AddCapture(await CreateRecordAsync(session.SessionId, sequence: 2));
        store.AddCapture(await CreateRecordAsync(session.SessionId, sequence: 3));

        Assert.DoesNotContain(store.Captures, capture => capture.CaptureId == first.CaptureId);
        Assert.Equal(2, store.Captures.Count);
    }

    [Fact]
    public async Task CaptureHistoryStore_ReportsApproximateMemoryEstimate()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();

        store.AddCapture(await CreateRecordAsync(session.SessionId, width: 4, height: 2));

        Assert.Equal(16, store.EstimatedMemoryBytes);
    }

    [Fact]
    public async Task CaptureHistoryStore_DeleteCaptureRemovesRecord()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var record = store.AddCapture(await CreateRecordAsync(session.SessionId));

        var deleted = store.DeleteCapture(record.CaptureId);

        Assert.True(deleted);
        Assert.Empty(store.Captures);
    }

    [Fact]
    public async Task CaptureHistoryStore_ClearRemovesCapturesAndSessions()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        store.AddCapture(await CreateRecordAsync(session.SessionId));

        store.Clear();

        Assert.Empty(store.Captures);
        Assert.Empty(store.Sessions);
        Assert.Null(store.ActiveSession);
    }

    [Fact]
    public async Task CaptureHistoryStore_UpdatesNotes()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var record = store.AddCapture(await CreateRecordAsync(session.SessionId));

        var updated = store.UpdateNotes(record.CaptureId, "  Slight overexposure  ");

        Assert.Equal("Slight overexposure", updated.Notes);
    }

    [Fact]
    public async Task CaptureHistoryStore_AddsNormalizedTagsAndPreventsDuplicates()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var record = store.AddCapture(await CreateRecordAsync(session.SessionId));

        store.AddTag(record.CaptureId, "  flat   field ");
        var updated = store.AddTag(record.CaptureId, "FLAT FIELD");

        Assert.Equal(["flat field"], updated.Tags);
    }

    [Fact]
    public async Task CaptureHistoryStore_RemovesTagsCaseInsensitively()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var record = store.AddCapture(await CreateRecordAsync(session.SessionId));
        store.AddTag(record.CaptureId, "reference");

        var updated = store.RemoveTag(record.CaptureId, "REFERENCE");

        Assert.Empty(updated.Tags);
    }

    [Fact]
    public void CaptureHistoryStore_RejectsEmptyTags()
    {
        var store = new CaptureHistoryStore();

        Assert.Throws<ArgumentException>(() => store.AddTag(Guid.NewGuid(), "   "));
    }

    [Fact]
    public async Task CaptureHistoryQuery_SearchesNotesTagsModelSerialSessionAndSource()
    {
        var store = new CaptureHistoryStore();
        var session = store.StartNewSession("Sample A");
        var record = store.AddCapture((await CreateRecordAsync(session.SessionId, source: CaptureSource.Live)) with { Notes = "calibration note", Tags = ["reference"] });

        Assert.Contains(record, store.Query(new CaptureHistoryFilter { SearchText = "calibration" }));
        Assert.Contains(record, store.Query(new CaptureHistoryFilter { SearchText = "reference" }));
        Assert.Contains(record, store.Query(new CaptureHistoryFilter { SearchText = "ORCA" }));
        Assert.Contains(record, store.Query(new CaptureHistoryFilter { SearchText = "SIM-0001" }));
        Assert.Contains(record, store.Query(new CaptureHistoryFilter { SearchText = "Sample A" }));
        Assert.Contains(record, store.Query(new CaptureHistoryFilter { SearchText = "Live" }));
    }

    [Fact]
    public async Task CaptureHistoryQuery_FiltersBySource()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        store.AddCapture(await CreateRecordAsync(session.SessionId, source: CaptureSource.Manual));
        var live = store.AddCapture(await CreateRecordAsync(session.SessionId, source: CaptureSource.Live));

        var result = store.Query(new CaptureHistoryFilter { Source = CaptureSource.Live });

        Assert.Equal([live], result);
    }

    [Fact]
    public async Task CaptureHistoryQuery_FiltersBySession()
    {
        var store = new CaptureHistoryStore();
        var first = store.StartNewSession("First");
        var firstRecord = store.AddCapture(await CreateRecordAsync(first.SessionId));
        var second = store.StartNewSession("Second");
        store.AddCapture(await CreateRecordAsync(second.SessionId));

        var result = store.Query(new CaptureHistoryFilter { SessionId = first.SessionId });

        Assert.Equal([firstRecord], result);
    }

    [Fact]
    public async Task CaptureHistoryQuery_FiltersTaggedOnly()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var tagged = store.AddCapture((await CreateRecordAsync(session.SessionId)) with { Tags = ["test"] });
        store.AddCapture(await CreateRecordAsync(session.SessionId));

        var result = store.Query(new CaptureHistoryFilter { TaggedOnly = true });

        Assert.Equal([tagged], result);
    }

    [Fact]
    public async Task CaptureHistoryQuery_FiltersSaturatedOnly()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        store.AddCapture(await CreateRecordAsync(session.SessionId, pixels: [1, 2, 3, 4]));
        var saturated = store.AddCapture(await CreateRecordAsync(session.SessionId, pixels: [65500, 65535, 1, 2]));

        var result = store.Query(new CaptureHistoryFilter { SaturatedOnly = true });

        Assert.Equal([saturated], result);
    }

    [Fact]
    public async Task CaptureHistoryQuery_SortsByExposure()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var high = store.AddCapture(await CreateRecordAsync(session.SessionId, exposure: TimeSpan.FromMilliseconds(100)));
        var low = store.AddCapture(await CreateRecordAsync(session.SessionId, exposure: TimeSpan.FromMilliseconds(10)));

        var result = store.Query(new CaptureHistoryFilter { SortMode = CaptureHistorySortMode.ExposureLowToHigh });

        Assert.Equal([low, high], result);
    }

    [Fact]
    public async Task CaptureHistoryQuery_SortsByMeanIntensity()
    {
        var store = new CaptureHistoryStore();
        var session = store.EnsureActiveSession();
        var bright = store.AddCapture(await CreateRecordAsync(session.SessionId, pixels: [100, 100, 100, 100]));
        var dim = store.AddCapture(await CreateRecordAsync(session.SessionId, pixels: [1, 1, 1, 1]));

        var result = store.Query(new CaptureHistoryFilter { SortMode = CaptureHistorySortMode.MeanIntensity });

        Assert.Equal([bright, dim], result);
    }

    [Fact]
    public async Task CaptureComparisonCalculator_ComputesMetadataDifferences()
    {
        var sessionId = Guid.NewGuid();
        var a = await CreateRecordAsync(sessionId, exposure: TimeSpan.FromMilliseconds(10), pixels: [0, 10, 20, 30]);
        var b = await CreateRecordAsync(sessionId, exposure: TimeSpan.FromMilliseconds(30), pixels: [10, 20, 30, 40]);

        var comparison = CaptureComparisonCalculator.Compare(a, b);

        Assert.Equal(TimeSpan.FromMilliseconds(20), comparison.ExposureDifference);
        Assert.Equal(10, comparison.MeanDifference);
        Assert.Equal(10, comparison.MeanAbsolutePixelDifference);
    }

    [Fact]
    public async Task CaptureHistoryStore_IsThreadSafeForConcurrentAdds()
    {
        var store = new CaptureHistoryStore(new CaptureHistoryOptions { MaximumCaptures = 100 });
        var session = store.EnsureActiveSession();

        await Task.WhenAll(Enumerable.Range(0, 25).Select(async index =>
        {
            store.AddCapture(await CreateRecordAsync(session.SessionId, sequence: index + 1));
        }));

        Assert.Equal(25, store.Captures.Count);
        Assert.Equal(25, store.Captures.Select(capture => capture.CaptureId).Distinct().Count());
    }

    [Fact]
    public void CaptureHistoryOptions_RejectsInvalidMaximum()
    {
        var options = new CaptureHistoryOptions { MaximumCaptures = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    private static async Task<SimulatedCameraService> CreateCameraAsync()
    {
        var camera = new SimulatedCameraService();
        var device = (await camera.DiscoverAsync()).Single();
        await camera.ConnectAsync(device.Id);
        return camera;
    }

    private static async Task<CaptureRecord> CreateRecordAsync(
        Guid sessionId,
        CaptureSource source = CaptureSource.Manual,
        TimeSpan? exposure = null,
        ushort[]? pixels = null,
        long sequence = 0,
        int width = 2,
        int height = 2)
    {
        var camera = await CreateCameraAsync();
        var actualExposure = exposure ?? TimeSpan.FromMilliseconds(25);
        await camera.SetExposureAsync(actualExposure);
        var factory = new CaptureRecordFactory(camera);
        return await factory.CreateAsync(
            sessionId,
            sequence,
            CreateFrame(pixels ?? Enumerable.Range(1, width * height).Select(value => (ushort)value).ToArray(), actualExposure, width, height),
            source);
    }

    private static CameraFrame CreateFrame(
        ushort[] pixels,
        TimeSpan? exposure = null,
        int width = 2,
        int height = 2)
    {
        return new CameraFrame
        {
            Width = width,
            Height = height,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = pixels,
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 1,
            Exposure = exposure ?? TimeSpan.FromMilliseconds(25)
        };
    }
}
