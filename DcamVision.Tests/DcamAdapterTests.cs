using System.Runtime.CompilerServices;
using DcamVision.Core;
using DcamVision.Dcam;
using DcamVision.Dcam.Acquisition;
using DcamVision.Dcam.Devices;
using DcamVision.Dcam.Errors;
using DcamVision.Dcam.Interop;
using DcamVision.Dcam.Properties;
using DcamVision.Dcam.Runtime;

namespace DcamVision.Tests;

public sealed class DcamAdapterTests
{
    [Fact]
    public void DcamRuntime_ChecksUnavailableRuntimeWithoutThrowing()
    {
        var runtime = new DcamRuntime(new FakeDcamNativeApi { RuntimeAvailable = false });

        Assert.False(runtime.Status.IsAvailable);
        Assert.Contains("not found", runtime.Status.UnavailableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DcamRuntime_InitializesOnlyOnce()
    {
        var native = new FakeDcamNativeApi();
        using var runtime = new DcamRuntime(native);

        runtime.Initialize();
        runtime.Initialize();

        Assert.True(runtime.IsInitialized);
        Assert.Equal(1, native.InitializeCount);
    }

    [Fact]
    public void DcamRuntime_UninitializesOnlyWhenInitialized()
    {
        var native = new FakeDcamNativeApi();
        using var runtime = new DcamRuntime(native);

        runtime.Uninitialize();
        runtime.Initialize();
        runtime.Uninitialize();
        runtime.Uninitialize();

        Assert.False(runtime.IsInitialized);
        Assert.Equal(1, native.UninitializeCount);
    }

    [Fact]
    public async Task DiscoverAsync_WhenRuntimeMissing_ReturnsNoHardwareCameras()
    {
        using var service = CreateService(new FakeDcamNativeApi { RuntimeAvailable = false });

        var devices = await service.DiscoverAsync();

        Assert.Empty(devices);
        Assert.Equal(CameraConnectionState.Disconnected, service.State);
    }

    [Fact]
    public async Task DiscoverAsync_MapsNativeDevices()
    {
        using var service = CreateService();

        var devices = await service.DiscoverAsync();

        var device = Assert.Single(devices);
        Assert.False(device.IsSimulated);
        Assert.Equal("dcam:0:CAM-123", device.Id);
        Assert.Equal("ORCA-Fusion BT", device.Model);
        Assert.Equal("CAM-123", device.SerialNumber);
    }

    [Fact]
    public async Task ConnectAsync_OpensSelectedDeviceAndReadsSettings()
    {
        var native = new FakeDcamNativeApi();
        using var service = CreateService(native);
        var device = Assert.Single(await service.DiscoverAsync());

        await service.ConnectAsync(device.Id);

        Assert.Equal(CameraConnectionState.Connected, service.State);
        Assert.Equal(device.Id, service.ConnectedDevice?.Id);
        Assert.Equal(TimeSpan.FromMilliseconds(25), service.CurrentSettings.Exposure);
        Assert.Equal(1, native.OpenCount);
    }

    [Fact]
    public async Task ConnectAsync_FailureClosesPartialHandle()
    {
        var native = new FakeDcamNativeApi { ThrowAfterOpenSettings = true };
        using var service = CreateService(native);
        var device = Assert.Single(await service.DiscoverAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConnectAsync(device.Id));

        Assert.Equal(1, native.CloseCount);
        Assert.Null(service.ConnectedDevice);
    }

    [Fact]
    public async Task DisconnectAsync_ClosesDevice()
    {
        var native = new FakeDcamNativeApi();
        using var service = await CreateConnectedServiceAsync(native);

        await service.DisconnectAsync();

        Assert.Equal(1, native.CloseCount);
        Assert.Null(service.ConnectedDevice);
    }

    [Fact]
    public async Task GetPropertiesAsync_ReturnsNativeProperties()
    {
        using var service = await CreateConnectedServiceAsync();

        var properties = await service.GetPropertiesAsync();

        Assert.Contains(properties, property => property.Id == "exposure.time");
        Assert.Contains(properties, property => property.Id == "trigger.mode");
    }

    [Fact]
    public async Task SetPropertyAsync_UsesAuthoritativeReadback()
    {
        var native = new FakeDcamNativeApi();
        using var service = await CreateConnectedServiceAsync(native);

        var result = await service.SetPropertyAsync("sensor.gain", 2.5);

        Assert.True(result.Success);
        Assert.Equal(2.5, result.UpdatedProperty?.Value);
        Assert.Equal(2.5, native.Gain);
    }

    [Fact]
    public async Task SetPropertyAsync_RejectsReadOnlyProperties()
    {
        using var service = await CreateConnectedServiceAsync();

        var result = await service.SetPropertyAsync("device.model", "Other");

        Assert.False(result.Success);
        Assert.Contains("read-only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetExposureAsync_ConvertsMillisecondsToNativeSeconds()
    {
        var native = new FakeDcamNativeApi();
        using var service = await CreateConnectedServiceAsync(native);

        await service.SetExposureAsync(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0.05, native.LastNativeExposureSeconds, 6);
        Assert.Equal(TimeSpan.FromMilliseconds(50), service.CurrentSettings.Exposure);
    }

    [Fact]
    public async Task ExposureRange_ComesFromNativePropertyMetadata()
    {
        using var service = await CreateConnectedServiceAsync();

        Assert.Equal(TimeSpan.FromMilliseconds(0.2), service.ExposureRange.Minimum);
        Assert.Equal(TimeSpan.FromSeconds(20), service.ExposureRange.Maximum);
    }

    [Fact]
    public void DcamPropertyMapper_MapsStableSemanticIds()
    {
        var property = DcamPropertyMapper.MapProperty(
            DcamConstants.PropertyExposureTime,
            "Exposure Time",
            0.025,
            FakeDcamNativeApi.CreateAttribute(DcamConstants.PropertyExposureTime, 0.0002, 20, 0.0001));

        Assert.Equal("exposure.time", property.Id);
        Assert.Equal(CameraPropertyCategories.Exposure, property.Category);
        Assert.Equal("ms", property.Unit);
    }

    [Fact]
    public void DcamPropertyMapper_MapsUnknownNativeIdsToDebuggableIds()
    {
        var property = DcamPropertyMapper.MapProperty(
            0x12345678,
            "Vendor Property",
            12,
            FakeDcamNativeApi.CreateAttribute(0x12345678, 0, 100, 1, DcamConstants.PropertyTypeLong));

        Assert.Equal("dcam.12345678", property.Id);
        Assert.Equal(CameraPropertyType.Integer, property.PropertyType);
    }

    [Fact]
    public void DcamPropertyMapper_MapsEnumerationOptions()
    {
        var property = DcamPropertyMapper.MapProperty(
            DcamConstants.PropertyTriggerSource,
            "Trigger Source",
            2,
            FakeDcamNativeApi.CreateAttribute(DcamConstants.PropertyTriggerSource, 1, 3, 1, DcamConstants.PropertyTypeMode),
            [new(1.0, "Internal"), new(2.0, "External")]);

        Assert.Equal(CameraPropertyType.Enumeration, property.PropertyType);
        Assert.Equal(2.0, property.Value);
        Assert.Equal(2, property.Options.Count);
    }

    [Fact]
    public void DcamErrorTranslator_MapsKnownAndUnknownErrors()
    {
        Assert.Equal("TIMEOUT", DcamErrorTranslator.Translate(DcamConstants.ErrorTimeout));
        Assert.Equal("Unknown DCAM error 0x81234567", DcamErrorTranslator.Translate(unchecked((int)0x81234567)));
    }

    [Fact]
    public void DcamFrameConverter_CopiesMono16WithRowPitch()
    {
        var raw = new DcamRawFrame
        {
            Width = 2,
            Height = 2,
            RowBytes = 6,
            PixelType = DcamConstants.PixelTypeMono16,
            Buffer = [1, 0, 2, 0, 99, 99, 3, 0, 4, 0, 99, 99],
            FrameStamp = 7
        };

        var frame = DcamFrameConverter.Convert(raw, TimeSpan.FromMilliseconds(5), 1);

        Assert.Equal([1, 2, 3, 4], frame.Pixels);
        Assert.Equal(7, frame.FrameNumber);
    }

    [Fact]
    public void DcamFrameConverter_CopiesBufferOwnership()
    {
        var buffer = new byte[] { 1, 0, 2, 0 };
        var raw = new DcamRawFrame
        {
            Width = 2,
            Height = 1,
            RowBytes = 4,
            PixelType = DcamConstants.PixelTypeMono16,
            Buffer = buffer,
            FrameStamp = 1
        };

        var frame = DcamFrameConverter.Convert(raw, TimeSpan.FromMilliseconds(5), 1);
        buffer[0] = 255;

        Assert.Equal((ushort)1, frame.Pixels[0]);
    }

    [Fact]
    public void DcamFrameConverter_MapsMono8ToMono16Storage()
    {
        var raw = new DcamRawFrame
        {
            Width = 2,
            Height = 1,
            RowBytes = 2,
            PixelType = DcamConstants.PixelTypeMono8,
            Buffer = [0, 255],
            FrameStamp = 4
        };

        var frame = DcamFrameConverter.Convert(raw, TimeSpan.FromMilliseconds(5), 1);

        Assert.Equal(CameraPixelFormat.Mono8, frame.PixelFormat);
        Assert.Equal([0, ushort.MaxValue], frame.Pixels);
    }

    [Fact]
    public void DcamFrameConverter_RejectsUnsupportedPixelType()
    {
        var raw = new DcamRawFrame
        {
            Width = 1,
            Height = 1,
            RowBytes = 2,
            PixelType = 999,
            Buffer = [0, 0],
            FrameStamp = 1
        };

        Assert.Throws<NotSupportedException>(() => DcamFrameConverter.Convert(raw, TimeSpan.FromMilliseconds(5), 1));
    }

    [Fact]
    public async Task CaptureAsync_ReturnsCopiedCameraFrame()
    {
        using var service = await CreateConnectedServiceAsync();

        var frame = await service.CaptureAsync();

        Assert.Equal(CameraPixelFormat.Mono16, frame.PixelFormat);
        Assert.Equal([10, 20, 30, 40], frame.Pixels);
    }

    [Fact]
    public async Task CaptureAsync_HonorsCancellation()
    {
        var native = new FakeDcamNativeApi { CaptureDelay = TimeSpan.FromSeconds(5) };
        using var service = await CreateConnectedServiceAsync(native);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CaptureAsync(cancellation.Token));
    }

    [Fact]
    public async Task StreamFramesAsync_ProducesSourceGapCompatibleFrameNumbers()
    {
        using var service = await CreateConnectedServiceAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var frameNumbers = new List<long>();

        await foreach (var frame in service.StreamFramesAsync(cancellation.Token))
        {
            frameNumbers.Add(frame.FrameNumber);
            if (frameNumbers.Count == 3)
            {
                break;
            }
        }

        Assert.Equal([1, 2, 4], frameNumbers);
        Assert.Equal(CameraConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task StreamFramesAsync_CancellationCleansUpStreamingState()
    {
        using var service = await CreateConnectedServiceAsync();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var frame in service.StreamFramesAsync(cancellation.Token))
            {
                Assert.True(frame.FrameNumber > 0);
                await cancellation.CancelAsync();
            }
        });

        Assert.Equal(CameraConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task CompositeCameraService_AutoDiscoveryKeepsSimulatorWhenDcamMissing()
    {
        var simulator = new SimulatedCameraService();
        using var dcam = CreateService(new FakeDcamNativeApi { RuntimeAvailable = false });
        var composite = new CompositeCameraService(simulator, dcam);

        var devices = await composite.DiscoverAsync();

        Assert.Single(devices);
        Assert.True(devices[0].IsSimulated);
    }

    [Fact]
    public async Task CompositeCameraService_CanSelectDcamBackendOnly()
    {
        var simulator = new SimulatedCameraService();
        using var dcam = CreateService();
        var composite = new CompositeCameraService(simulator, dcam)
        {
            SelectedBackend = CameraBackend.HamamatsuDcam
        };

        var devices = await composite.DiscoverAsync();

        var device = Assert.Single(devices);
        Assert.False(device.IsSimulated);
    }

    [Fact]
    public async Task CompositeCameraService_RoutesConnectionToDiscoveredOwner()
    {
        var simulator = new SimulatedCameraService();
        using var dcam = CreateService();
        var composite = new CompositeCameraService(simulator, dcam);
        var hardware = (await composite.DiscoverAsync()).First(device => !device.IsSimulated);

        await composite.ConnectAsync(hardware.Id);

        Assert.Equal(hardware.Id, composite.ConnectedDevice?.Id);
        await composite.DisconnectAsync();
    }

    [Fact(Skip = "Hardware test harness. Set DCAM_RUN_HARDWARE_TESTS=1 and remove Skip locally when a Hamamatsu camera/runtime is available.")]
    [Trait("Category", "Hardware")]
    public async Task HardwareHarness_CanDiscoverConnectCaptureAndDisconnect()
    {
        if (Environment.GetEnvironmentVariable("DCAM_RUN_HARDWARE_TESTS") != "1")
        {
            return;
        }

        using var native = new DcamNativeApi();
        using var runtime = new DcamRuntime(native);
        using var service = new DcamCameraService(runtime, native);
        var device = (await service.DiscoverAsync()).FirstOrDefault();
        Assert.NotNull(device);
        await service.ConnectAsync(device.Id);
        var frame = await service.CaptureAsync();
        Assert.True(frame.Pixels.Length > 0);
        await service.DisconnectAsync();
    }

    private static DcamCameraService CreateService(FakeDcamNativeApi? native = null)
    {
        native ??= new FakeDcamNativeApi();
        return new DcamCameraService(new DcamRuntime(native), native);
    }

    private static async Task<DcamCameraService> CreateConnectedServiceAsync(FakeDcamNativeApi? native = null)
    {
        var service = CreateService(native);
        var device = Assert.Single(await service.DiscoverAsync());
        await service.ConnectAsync(device.Id);
        return service;
    }

    private sealed class FakeDcamNativeApi : IDcamNativeApi
    {
        private readonly IntPtr _handle = new(42);
        private int _streamIndex;

        public bool RuntimeAvailable { get; init; } = true;

        public bool ThrowAfterOpenSettings { get; init; }

        public TimeSpan CaptureDelay { get; init; }

        public int InitializeCount { get; private set; }

        public int UninitializeCount { get; private set; }

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public double Gain { get; private set; } = 1.0;

        public double LastNativeExposureSeconds { get; private set; } = 0.025;

        public DcamRuntimeStatus CheckRuntime()
        {
            return RuntimeAvailable
                ? DcamRuntimeStatus.Available(1)
                : DcamRuntimeStatus.Unavailable("dcamapi.dll was not found.");
        }

        public int Initialize(out int deviceCount)
        {
            InitializeCount++;
            deviceCount = 1;
            return DcamConstants.Success;
        }

        public int Uninitialize()
        {
            UninitializeCount++;
            return DcamConstants.Success;
        }

        public IReadOnlyList<DcamDeviceInfo> EnumerateDevices(int deviceCount)
        {
            return
            [
                new DcamDeviceInfo
                {
                    Index = 0,
                    DeviceId = "dcam:0:CAM-123",
                    Vendor = "Hamamatsu",
                    Model = "ORCA-Fusion BT",
                    CameraId = "CAM-123",
                    DriverVersion = "1.2.3",
                    DcamApiVersion = "24.4"
                }
            ];
        }

        public IntPtr OpenDevice(int index)
        {
            OpenCount++;
            return _handle;
        }

        public void CloseDevice(IntPtr deviceHandle)
        {
            CloseCount++;
        }

        public IReadOnlyList<CameraProperty> EnumerateProperties(IntPtr deviceHandle)
        {
            return [ExposureProperty(), GainProperty(), TriggerProperty(), ModelProperty()];
        }

        public CameraProperty? GetProperty(IntPtr deviceHandle, string propertyId)
        {
            return EnumerateProperties(deviceHandle).FirstOrDefault(property => string.Equals(property.Id, propertyId, StringComparison.OrdinalIgnoreCase));
        }

        public CameraProperty SetProperty(IntPtr deviceHandle, CameraProperty property, object value)
        {
            if (property.Id == "exposure.time")
            {
                LastNativeExposureSeconds = Convert.ToDouble(value) / 1000.0;
                return ExposureProperty() with { Value = LastNativeExposureSeconds * 1000.0 };
            }

            if (property.Id == "sensor.gain")
            {
                Gain = Convert.ToDouble(value);
                return GainProperty() with { Value = Gain };
            }

            return property with { Value = value };
        }

        public TimeSpan GetExposure(IntPtr deviceHandle)
        {
            return TimeSpan.FromSeconds(LastNativeExposureSeconds);
        }

        public CameraExposureRange GetExposureRange(IntPtr deviceHandle)
        {
            return new CameraExposureRange(TimeSpan.FromMilliseconds(0.2), TimeSpan.FromSeconds(20));
        }

        public CaptureSettings GetCaptureSettings(IntPtr deviceHandle)
        {
            if (ThrowAfterOpenSettings)
            {
                throw new InvalidOperationException("settings failed");
            }

            return new CaptureSettings
            {
                Exposure = TimeSpan.FromSeconds(LastNativeExposureSeconds),
                Width = 2,
                Height = 2,
                PixelFormat = CameraPixelFormat.Mono16,
                Gain = Gain
            };
        }

        public DcamRawFrame CaptureFrame(IntPtr deviceHandle, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (CaptureDelay > TimeSpan.Zero)
            {
                Task.Delay(CaptureDelay, cancellationToken).GetAwaiter().GetResult();
            }

            return RawFrame(1);
        }

        public async IAsyncEnumerable<DcamRawFrame> StreamFramesAsync(
            IntPtr deviceHandle,
            TimeSpan timeout,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var stamps = new[] { 1L, 2L, 4L };
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return RawFrame(stamps[Math.Min(_streamIndex, stamps.Length - 1)]);
                _streamIndex++;
                await Task.Delay(1, cancellationToken);
            }
        }

        public static DcamPropertyAttribute CreateAttribute(
            int propertyId,
            double minimum,
            double maximum,
            double step,
            int group = DcamConstants.PropertyTypeReal,
            bool writable = true)
        {
            return new DcamPropertyAttribute
            {
                PropertyId = propertyId,
                Group = group,
                Attribute = DcamConstants.PropertyAttributeReadable | (writable ? DcamConstants.PropertyAttributeWritable : 0),
                Minimum = minimum,
                Maximum = maximum,
                Step = step
            };
        }

        private static DcamRawFrame RawFrame(long stamp)
        {
            return new DcamRawFrame
            {
                Width = 2,
                Height = 2,
                RowBytes = 6,
                PixelType = DcamConstants.PixelTypeMono16,
                Buffer = [10, 0, 20, 0, 99, 99, 30, 0, 40, 0, 99, 99],
                FrameStamp = stamp,
                Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(stamp)
            };
        }

        private CameraProperty ExposureProperty()
        {
            return new CameraProperty
            {
                Id = "exposure.time",
                DisplayName = "Exposure Time",
                Category = CameraPropertyCategories.Exposure,
                PropertyType = CameraPropertyType.FloatingPoint,
                Value = LastNativeExposureSeconds * 1000.0,
                Minimum = 0.2,
                Maximum = 20_000.0,
                Step = 0.1,
                Unit = "ms",
                Access = CameraPropertyAccess.Read | CameraPropertyAccess.Write | CameraPropertyAccess.WriteWhileStreaming
            };
        }

        private CameraProperty GainProperty()
        {
            return new CameraProperty
            {
                Id = "sensor.gain",
                DisplayName = "Gain",
                Category = CameraPropertyCategories.Sensor,
                PropertyType = CameraPropertyType.FloatingPoint,
                Value = Gain,
                Minimum = 1.0,
                Maximum = 8.0,
                Step = 0.1,
                Unit = "x",
                Access = CameraPropertyAccess.Read | CameraPropertyAccess.Write | CameraPropertyAccess.WriteWhileStreaming
            };
        }

        private static CameraProperty TriggerProperty()
        {
            return new CameraProperty
            {
                Id = "trigger.mode",
                DisplayName = "Trigger Mode",
                Category = CameraPropertyCategories.Trigger,
                PropertyType = CameraPropertyType.Enumeration,
                Value = "Internal",
                Options = [new("Internal", "Internal"), new("External", "External")],
                Access = CameraPropertyAccess.Read | CameraPropertyAccess.Write | CameraPropertyAccess.WriteWhileStreaming
            };
        }

        private static CameraProperty ModelProperty()
        {
            return new CameraProperty
            {
                Id = "device.model",
                DisplayName = "Model",
                Category = CameraPropertyCategories.Device,
                PropertyType = CameraPropertyType.Text,
                Value = "ORCA-Fusion BT",
                Access = CameraPropertyAccess.Read
            };
        }
    }
}
