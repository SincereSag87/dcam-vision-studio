using System.Runtime.CompilerServices;

namespace DcamVision.Core;

public sealed class SimulatedCameraService : ICameraService
{
    private static readonly CameraDevice SimulatedDevice = new(
        "simulated-hamamatsu-orca",
        "Simulated Hamamatsu ORCA",
        "Hamamatsu",
        "ORCA-Sim",
        "SIM-0001",
        IsSimulated: true);

    private readonly object _syncRoot = new();
    private readonly SimulatedCameraOptions _options;
    private CaptureSettings _settings = new();
    private long _frameNumber;
    private string _triggerPolarity = "Rising Edge";
    private int _binning = 1;
    private string _readoutSpeed = "Normal";
    private bool _coolingEnabled = true;
    private string _fanMode = "Low";

    public SimulatedCameraService()
        : this(new SimulatedCameraOptions())
    {
    }

    public SimulatedCameraService(SimulatedCameraOptions options)
    {
        if (options.FramesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Simulated frame rate must be greater than zero.");
        }

        _options = options;
    }

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;

    public CameraDevice? ConnectedDevice { get; private set; }

    public CaptureSettings CurrentSettings => _settings;

    public CameraExposureRange ExposureRange { get; } = CameraExposureRange.Default;

    public async Task<IReadOnlyList<CameraDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(CameraConnectionState.Discovering);

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(75), cancellationToken).ConfigureAwait(false);
            return [SimulatedDevice];
        }
        finally
        {
            if (ConnectedDevice is null)
            {
                SetState(CameraConnectionState.Disconnected);
            }
            else
            {
                SetState(CameraConnectionState.Connected);
            }
        }
    }

    public async Task ConnectAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            throw new ArgumentException("Camera id is required.", nameof(cameraId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(cameraId, SimulatedDevice.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Camera '{cameraId}' was not discovered by the simulator.");
        }

        if (ConnectedDevice is not null)
        {
            throw new InvalidOperationException("A camera is already connected.");
        }

        SetState(CameraConnectionState.Connecting);

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            ConnectedDevice = SimulatedDevice;
            _frameNumber = 0;
            SetState(CameraConnectionState.Connected);
        }
        catch
        {
            SetState(CameraConnectionState.Disconnected);
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectedDevice = null;
        SetState(CameraConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public Task<CameraFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        var frameNumber = Interlocked.Increment(ref _frameNumber);
        var frame = GenerateFrame(frameNumber);
        frame.Validate();
        return Task.FromResult(frame);
    }

    public async IAsyncEnumerable<CameraFrame> StreamFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        SetState(CameraConnectionState.Streaming);

        var interval = TimeSpan.FromSeconds(1.0 / _options.FramesPerSecond);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return await CaptureAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ConnectedDevice is not null)
            {
                SetState(CameraConnectionState.Connected);
            }
        }
    }

    public Task SetExposureAsync(TimeSpan exposure, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ExposureRange.TryValidate(exposure, out var errorMessage))
        {
            throw new ArgumentOutOfRangeException(nameof(exposure), errorMessage);
        }

        _settings = _settings with { Exposure = exposure };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildProperties());
    }

    public Task<CameraProperty?> GetPropertyAsync(string propertyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var property = BuildProperties().FirstOrDefault(candidate => string.Equals(candidate.Id, propertyId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(property);
    }

    public Task<CameraPropertyUpdateResult> SetPropertyAsync(
        string propertyId,
        object value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        var property = BuildProperties().FirstOrDefault(candidate => string.Equals(candidate.Id, propertyId, StringComparison.OrdinalIgnoreCase));
        if (property is null)
        {
            return Task.FromResult(CameraPropertyUpdateResult.Failed($"Camera property '{propertyId}' was not found."));
        }

        var validation = CameraPropertyValidator.ValidateWrite(property, value, State is CameraConnectionState.Streaming);
        if (!validation.Success || validation.UpdatedProperty is null)
        {
            return Task.FromResult(validation);
        }

        ApplyProperty(validation.UpdatedProperty);
        var updated = BuildProperties().First(candidate => string.Equals(candidate.Id, property.Id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(CameraPropertyUpdateResult.Updated(updated));
    }

    private IReadOnlyList<CameraProperty> BuildProperties()
    {
        var read = CameraPropertyAccess.Read;
        var readWrite = CameraPropertyAccess.Read | CameraPropertyAccess.Write;
        var writeLive = CameraPropertyAccess.Read | CameraPropertyAccess.Write | CameraPropertyAccess.WriteWhileStreaming;
        var triggerExternal = string.Equals(_settings.TriggerMode, "External", StringComparison.Ordinal);

        return
        [
            new CameraProperty
            {
                Id = "acquisition.frameRate",
                DisplayName = "Frame Rate",
                Description = "Nominal simulator live acquisition frame rate.",
                Category = CameraPropertyCategories.Acquisition,
                DisplayOrder = 10,
                PropertyType = CameraPropertyType.FloatingPoint,
                Value = _options.FramesPerSecond,
                Minimum = 1.0,
                Maximum = 240.0,
                Step = 1.0,
                Unit = "fps",
                Access = read
            },
            new CameraProperty
            {
                Id = "exposure.time",
                DisplayName = "Exposure Time",
                Description = "Camera integration time.",
                Category = CameraPropertyCategories.Exposure,
                DisplayOrder = 20,
                PropertyType = CameraPropertyType.FloatingPoint,
                Value = _settings.Exposure.TotalMilliseconds,
                Minimum = ExposureRange.Minimum.TotalMilliseconds,
                Maximum = ExposureRange.Maximum.TotalMilliseconds,
                Step = 0.1,
                Unit = "ms",
                Access = writeLive
            },
            new CameraProperty
            {
                Id = "sensor.gain",
                DisplayName = "Gain",
                Description = "Sensor amplification.",
                Category = CameraPropertyCategories.Sensor,
                DisplayOrder = 30,
                PropertyType = CameraPropertyType.FloatingPoint,
                Value = _settings.Gain,
                Minimum = 1.0,
                Maximum = 8.0,
                Step = 0.1,
                Unit = "x",
                Access = writeLive
            },
            new CameraProperty
            {
                Id = "sensor.temperature",
                DisplayName = "Sensor Temperature",
                Description = "Deterministic simulated sensor temperature.",
                Category = CameraPropertyCategories.Sensor,
                DisplayOrder = 40,
                PropertyType = CameraPropertyType.FloatingPoint,
                Value = _coolingEnabled ? -12.4 : 21.0,
                Unit = "°C",
                Access = read
            },
            new CameraProperty
            {
                Id = "image.width",
                DisplayName = "Width",
                Description = "Frame width.",
                Category = CameraPropertyCategories.Image,
                DisplayOrder = 50,
                PropertyType = CameraPropertyType.Integer,
                Value = _settings.Width,
                Minimum = 64,
                Maximum = 4096,
                Step = 1,
                Unit = "px",
                Access = readWrite
            },
            new CameraProperty
            {
                Id = "image.height",
                DisplayName = "Height",
                Description = "Frame height.",
                Category = CameraPropertyCategories.Image,
                DisplayOrder = 60,
                PropertyType = CameraPropertyType.Integer,
                Value = _settings.Height,
                Minimum = 64,
                Maximum = 4096,
                Step = 1,
                Unit = "px",
                Access = readWrite
            },
            new CameraProperty
            {
                Id = "image.pixelFormat",
                DisplayName = "Pixel Format",
                Description = "Pixel encoding used by captured frames.",
                Category = CameraPropertyCategories.Image,
                DisplayOrder = 70,
                PropertyType = CameraPropertyType.Enumeration,
                Value = _settings.PixelFormat.ToString(),
                Options = [new("Mono16", "Mono16")],
                Access = read
            },
            new CameraProperty
            {
                Id = "trigger.mode",
                DisplayName = "Trigger Mode",
                Description = "Acquisition trigger source.",
                Category = CameraPropertyCategories.Trigger,
                DisplayOrder = 80,
                PropertyType = CameraPropertyType.Enumeration,
                Value = _settings.TriggerMode,
                Options = [new("Internal", "Internal"), new("External", "External"), new("Software", "Software")],
                Access = readWrite
            },
            new CameraProperty
            {
                Id = "trigger.polarity",
                DisplayName = "Trigger Polarity",
                Description = "External trigger edge polarity.",
                Category = CameraPropertyCategories.Trigger,
                DisplayOrder = 90,
                PropertyType = CameraPropertyType.Enumeration,
                Value = _triggerPolarity,
                Options = [new("Rising Edge", "Rising Edge"), new("Falling Edge", "Falling Edge")],
                Access = triggerExternal ? readWrite : read,
                IsAvailable = triggerExternal,
                AvailabilityReason = "Trigger Polarity is available when Trigger Mode is External."
            },
            new CameraProperty
            {
                Id = "image.binning",
                DisplayName = "Binning",
                Description = "Sensor binning factor.",
                Category = CameraPropertyCategories.Image,
                DisplayOrder = 100,
                PropertyType = CameraPropertyType.Enumeration,
                Value = _binning,
                Options = [new(1, "1x1"), new(2, "2x2"), new(4, "4x4")],
                Access = readWrite
            },
            new CameraProperty
            {
                Id = "sensor.readoutSpeed",
                DisplayName = "Readout Speed",
                Description = "Readout speed profile.",
                Category = CameraPropertyCategories.Sensor,
                DisplayOrder = 110,
                PropertyType = CameraPropertyType.Enumeration,
                Value = _readoutSpeed,
                Options = [new("Slow", "Slow"), new("Normal", "Normal"), new("Fast", "Fast")],
                Access = readWrite
            },
            new CameraProperty
            {
                Id = "sensor.coolingEnabled",
                DisplayName = "Cooling Enabled",
                Description = "Controls simulated sensor cooling.",
                Category = CameraPropertyCategories.Sensor,
                DisplayOrder = 120,
                PropertyType = CameraPropertyType.Boolean,
                Value = _coolingEnabled,
                Access = writeLive
            },
            new CameraProperty
            {
                Id = "device.fanMode",
                DisplayName = "Fan Mode",
                Description = "Cooling fan behavior.",
                Category = CameraPropertyCategories.Device,
                DisplayOrder = 130,
                PropertyType = CameraPropertyType.Enumeration,
                Value = _fanMode,
                Options = [new("Off", "Off"), new("Low", "Low"), new("High", "High")],
                Access = _coolingEnabled ? readWrite : read,
                IsAvailable = _coolingEnabled,
                AvailabilityReason = "Fan Mode is available when Cooling Enabled is on."
            },
            new CameraProperty
            {
                Id = "device.manufacturer",
                DisplayName = "Manufacturer",
                Description = "Camera manufacturer.",
                Category = CameraPropertyCategories.Device,
                DisplayOrder = 140,
                PropertyType = CameraPropertyType.Text,
                Value = SimulatedDevice.Manufacturer,
                Access = read
            },
            new CameraProperty
            {
                Id = "device.model",
                DisplayName = "Model",
                Description = "Camera model.",
                Category = CameraPropertyCategories.Device,
                DisplayOrder = 150,
                PropertyType = CameraPropertyType.Text,
                Value = SimulatedDevice.Model,
                Access = read
            },
            new CameraProperty
            {
                Id = "device.serialNumber",
                DisplayName = "Serial Number",
                Description = "Camera serial number.",
                Category = CameraPropertyCategories.Device,
                DisplayOrder = 160,
                PropertyType = CameraPropertyType.Text,
                Value = SimulatedDevice.SerialNumber,
                Access = read
            },
            new CameraProperty
            {
                Id = "device.firmwareVersion",
                DisplayName = "Firmware Version",
                Description = "Simulated camera firmware version.",
                Category = CameraPropertyCategories.Device,
                DisplayOrder = 170,
                PropertyType = CameraPropertyType.Text,
                Value = "SIM-2026.09",
                Access = read
            }
        ];
    }

    private void ApplyProperty(CameraProperty property)
    {
        switch (property.Id)
        {
            case "exposure.time":
                _settings = _settings with { Exposure = TimeSpan.FromMilliseconds(Convert.ToDouble(property.Value)) };
                break;
            case "sensor.gain":
                _settings = _settings with { Gain = Convert.ToDouble(property.Value) };
                break;
            case "image.width":
                _settings = _settings with { Width = Convert.ToInt32(property.Value) };
                break;
            case "image.height":
                _settings = _settings with { Height = Convert.ToInt32(property.Value) };
                break;
            case "trigger.mode":
                _settings = _settings with { TriggerMode = Convert.ToString(property.Value) ?? "Internal" };
                break;
            case "trigger.polarity":
                _triggerPolarity = Convert.ToString(property.Value) ?? "Rising Edge";
                break;
            case "image.binning":
                _binning = Convert.ToInt32(property.Value);
                break;
            case "sensor.readoutSpeed":
                _readoutSpeed = Convert.ToString(property.Value) ?? "Normal";
                break;
            case "sensor.coolingEnabled":
                _coolingEnabled = Convert.ToBoolean(property.Value);
                if (!_coolingEnabled)
                {
                    _fanMode = "Off";
                }

                break;
            case "device.fanMode":
                _fanMode = Convert.ToString(property.Value) ?? "Low";
                break;
        }
    }

    private CameraFrame GenerateFrame(long frameNumber)
    {
        var settings = _settings;
        var pixels = new ushort[settings.Width * settings.Height];
        var exposureScale = Math.Clamp(settings.Exposure.TotalMilliseconds / CaptureSettings.DefaultExposure.TotalMilliseconds, 0.1, 8.0);

        for (var y = 0; y < settings.Height; y++)
        {
            for (var x = 0; x < settings.Width; x++)
            {
                var gradient = (double)x / Math.Max(1, settings.Width - 1);
                var vertical = (double)y / Math.Max(1, settings.Height - 1);
                var wave = (Math.Sin((x + frameNumber * 5) * 0.045) + Math.Cos((y - frameNumber * 3) * 0.035) + 2.0) / 4.0;
                var target = Math.Clamp((gradient * 0.35 + vertical * 0.20 + wave * 0.45) * exposureScale, 0.0, 1.0);
                pixels[y * settings.Width + x] = (ushort)Math.Round(target * ushort.MaxValue);
            }
        }

        return new CameraFrame
        {
            Width = settings.Width,
            Height = settings.Height,
            PixelFormat = settings.PixelFormat,
            Pixels = pixels,
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = frameNumber,
            Exposure = settings.Exposure
        };
    }

    private void EnsureConnected()
    {
        if (ConnectedDevice is null)
        {
            throw new InvalidOperationException("No camera is connected.");
        }
    }

    private void SetState(CameraConnectionState state)
    {
        lock (_syncRoot)
        {
            State = state;
        }
    }
}
