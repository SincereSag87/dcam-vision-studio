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
    private CaptureSettings _settings = new();
    private long _frameNumber;

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;

    public CameraDevice? ConnectedDevice { get; private set; }

    public CaptureSettings CurrentSettings => _settings;

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

        var interval = TimeSpan.FromMilliseconds(66);

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

        if (exposure <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(exposure), "Exposure must be greater than zero.");
        }

        if (exposure > TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(nameof(exposure), "Exposure must be 10 seconds or less.");
        }

        _settings = _settings with { Exposure = exposure };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<CameraProperty> properties =
        [
            new("exposure", "Exposure Time", _settings.Exposure.TotalMilliseconds, "ms", 0.1, 10_000.0),
            new("gain", "Gain", _settings.Gain, "x", 1.0, 8.0),
            new("width", "Width", _settings.Width, "px", 64, 4096, IsReadOnly: true),
            new("height", "Height", _settings.Height, "px", 64, 4096, IsReadOnly: true),
            new("pixelFormat", "Pixel Format", _settings.PixelFormat, null, IsReadOnly: true),
            new("triggerMode", "Trigger Mode", _settings.TriggerMode)
        ];

        return Task.FromResult(properties);
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
