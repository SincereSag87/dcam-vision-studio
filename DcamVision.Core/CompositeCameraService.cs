namespace DcamVision.Core;

public sealed class CompositeCameraService : ICameraService, ICameraBackendSelector
{
    private readonly ICameraService _simulator;
    private readonly ICameraService _dcam;
    private readonly Dictionary<string, ICameraService> _deviceOwners = new(StringComparer.OrdinalIgnoreCase);
    private ICameraService? _activeService;
    private CameraBackend _selectedBackend;

    public CompositeCameraService(ICameraService simulator, ICameraService dcam)
    {
        _simulator = simulator;
        _dcam = dcam;
    }

    public CameraBackend SelectedBackend
    {
        get => _selectedBackend;
        set
        {
            if (ConnectedDevice is not null && value != _selectedBackend)
            {
                throw new InvalidOperationException("Disconnect the current camera before changing camera source.");
            }

            _selectedBackend = value;
        }
    }

    public IReadOnlyList<CameraBackend> AvailableBackends { get; } =
    [
        CameraBackend.Auto,
        CameraBackend.Simulator,
        CameraBackend.HamamatsuDcam
    ];

    public CameraConnectionState State => ActiveOrSelected.State;

    public CameraDevice? ConnectedDevice => _activeService?.ConnectedDevice;

    public CaptureSettings CurrentSettings => ActiveOrSelected.CurrentSettings;

    public CameraExposureRange ExposureRange => ActiveOrSelected.ExposureRange;

    private ICameraService ActiveOrSelected => _activeService ?? SelectedServices().FirstOrDefault() ?? _simulator;

    public async Task<IReadOnlyList<CameraDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _deviceOwners.Clear();
        var devices = new List<CameraDevice>();

        foreach (var service in SelectedServices())
        {
            var discovered = await service.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            foreach (var device in discovered)
            {
                _deviceOwners[device.Id] = service;
                devices.Add(device);
            }
        }

        return devices;
    }

    public async Task ConnectAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        if (ConnectedDevice is not null)
        {
            throw new InvalidOperationException("A camera is already connected.");
        }

        if (!_deviceOwners.TryGetValue(cameraId, out var service))
        {
            await DiscoverAsync(cancellationToken).ConfigureAwait(false);
            if (!_deviceOwners.TryGetValue(cameraId, out service))
            {
                throw new InvalidOperationException($"Camera '{cameraId}' was not discovered.");
            }
        }

        await service.ConnectAsync(cameraId, cancellationToken).ConfigureAwait(false);
        _activeService = service;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_activeService is null)
        {
            return;
        }

        await _activeService.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        _activeService = null;
    }

    public Task<CameraFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return RequireConnected().CaptureAsync(cancellationToken);
    }

    public IAsyncEnumerable<CameraFrame> StreamFramesAsync(CancellationToken cancellationToken = default)
    {
        return RequireConnected().StreamFramesAsync(cancellationToken);
    }

    public Task SetExposureAsync(TimeSpan exposure, CancellationToken cancellationToken = default)
    {
        return RequireConnected().SetExposureAsync(exposure, cancellationToken);
    }

    public Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        return ActiveOrSelected.GetPropertiesAsync(cancellationToken);
    }

    public Task<CameraProperty?> GetPropertyAsync(string propertyId, CancellationToken cancellationToken = default)
    {
        return ActiveOrSelected.GetPropertyAsync(propertyId, cancellationToken);
    }

    public Task<CameraPropertyUpdateResult> SetPropertyAsync(
        string propertyId,
        object value,
        CancellationToken cancellationToken = default)
    {
        return RequireConnected().SetPropertyAsync(propertyId, value, cancellationToken);
    }

    private IEnumerable<ICameraService> SelectedServices()
    {
        return SelectedBackend switch
        {
            CameraBackend.Simulator => [_simulator],
            CameraBackend.HamamatsuDcam => [_dcam],
            _ => [_dcam, _simulator]
        };
    }

    private ICameraService RequireConnected()
    {
        return _activeService ?? throw new InvalidOperationException("No camera is connected.");
    }
}
