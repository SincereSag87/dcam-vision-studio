using System.Runtime.CompilerServices;
using DcamVision.Core;
using DcamVision.Dcam.Acquisition;
using DcamVision.Dcam.Devices;
using DcamVision.Dcam.Errors;
using DcamVision.Dcam.Interop;
using DcamVision.Dcam.Properties;
using DcamVision.Dcam.Runtime;
using Microsoft.Extensions.Logging;

namespace DcamVision.Dcam;

public sealed class DcamCameraService : ICameraService, IDisposable
{
    private readonly IDcamRuntime _runtime;
    private readonly IDcamNativeApi _nativeApi;
    private readonly DcamAcquisitionOptions _options;
    private readonly ILogger<DcamCameraService>? _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private IReadOnlyList<DcamDeviceInfo> _discoveredDevices = [];
    private DcamDeviceInfo? _connectedInfo;
    private IntPtr _deviceHandle;
    private CaptureSettings _settings = new();
    private CameraExposureRange _exposureRange = CameraExposureRange.Default;
    private long _fallbackFrameNumber;
    private bool _disposed;

    public DcamCameraService(
        IDcamRuntime runtime,
        IDcamNativeApi nativeApi,
        DcamAcquisitionOptions? options = null,
        ILogger<DcamCameraService>? logger = null)
    {
        _runtime = runtime;
        _nativeApi = nativeApi;
        _options = options ?? new DcamAcquisitionOptions();
        _options.Validate();
        _logger = logger;
    }

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;

    public CameraDevice? ConnectedDevice { get; private set; }

    public CaptureSettings CurrentSettings => _settings;

    public CameraExposureRange ExposureRange => _exposureRange;

    public DcamRuntimeStatus RuntimeStatus => _runtime.Status;

    public async Task<IReadOnlyList<CameraDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(CameraConnectionState.Discovering);

        try
        {
            var status = _runtime.Initialize();
            if (!status.IsAvailable)
            {
                _logger?.LogInformation("DCAM discovery skipped: {Reason}", status.UnavailableReason);
                _discoveredDevices = [];
                return [];
            }

            _discoveredDevices = await Task.Run(() => _nativeApi.EnumerateDevices(status.DeviceCount), cancellationToken).ConfigureAwait(false);
            return _discoveredDevices
                .Select(device => new CameraDevice(
                    device.DeviceId,
                    string.IsNullOrWhiteSpace(device.Model) ? $"Hamamatsu DCAM Camera {device.Index}" : device.Model,
                    string.IsNullOrWhiteSpace(device.Vendor) ? "Hamamatsu" : device.Vendor,
                    string.IsNullOrWhiteSpace(device.Model) ? "DCAM Camera" : device.Model,
                    string.IsNullOrWhiteSpace(device.CameraId) ? $"DCAM-{device.Index}" : device.CameraId,
                    IsSimulated: false))
                .ToArray();
        }
        finally
        {
            SetState(ConnectedDevice is null ? CameraConnectionState.Disconnected : CameraConnectionState.Connected);
        }
    }

    public async Task ConnectAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            throw new ArgumentException("Camera id is required.", nameof(cameraId));
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ConnectedDevice is not null)
            {
                throw new InvalidOperationException("A DCAM camera is already connected.");
            }

            var status = _runtime.Initialize();
            if (!status.IsAvailable)
            {
                throw new InvalidOperationException(status.UnavailableReason ?? "Hamamatsu DCAM runtime is unavailable.");
            }

            if (_discoveredDevices.Count == 0)
            {
                _discoveredDevices = _nativeApi.EnumerateDevices(status.DeviceCount);
            }

            var device = _discoveredDevices.FirstOrDefault(candidate => string.Equals(candidate.DeviceId, cameraId, StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                throw new InvalidOperationException($"DCAM camera '{cameraId}' was not discovered.");
            }

            SetState(CameraConnectionState.Connecting);
            try
            {
                _deviceHandle = _nativeApi.OpenDevice(device.Index);
                _connectedInfo = device;
                _settings = _nativeApi.GetCaptureSettings(_deviceHandle);
                _exposureRange = _nativeApi.GetExposureRange(_deviceHandle);
                _fallbackFrameNumber = 0;
                ConnectedDevice = new CameraDevice(
                    device.DeviceId,
                    device.Model,
                    device.Vendor,
                    device.Model,
                    string.IsNullOrWhiteSpace(device.CameraId) ? $"DCAM-{device.Index}" : device.CameraId,
                    IsSimulated: false);
                SetState(CameraConnectionState.Connected);
                _logger?.LogInformation("Connected to DCAM camera {CameraId}.", device.DeviceId);
            }
            catch
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    _nativeApi.CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }

                _connectedInfo = null;
                ConnectedDevice = null;
                SetState(CameraConnectionState.Disconnected);
                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CloseDevice();
            SetState(CameraConnectionState.Disconnected);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<CameraFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            var timeout = CalculateTimeout();
            var rawFrame = await Task.Run(() => _nativeApi.CaptureFrame(_deviceHandle, timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
            var frame = DcamFrameConverter.Convert(rawFrame, _settings.Exposure, Interlocked.Increment(ref _fallbackFrameNumber));
            return frame;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async IAsyncEnumerable<CameraFrame> StreamFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            SetState(CameraConnectionState.Streaming);
        }
        finally
        {
            _operationLock.Release();
        }

        try
        {
            await foreach (var rawFrame in _nativeApi.StreamFramesAsync(_deviceHandle, CalculateTimeout(), cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return DcamFrameConverter.Convert(rawFrame, _settings.Exposure, Interlocked.Increment(ref _fallbackFrameNumber));
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

    public async Task SetExposureAsync(TimeSpan exposure, CancellationToken cancellationToken = default)
    {
        if (!ExposureRange.TryValidate(exposure, out var errorMessage))
        {
            throw new ArgumentOutOfRangeException(nameof(exposure), errorMessage);
        }

        var result = await SetPropertyAsync("exposure.time", exposure.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }

    public Task<IReadOnlyList<CameraProperty>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();
        return Task.FromResult(_nativeApi.EnumerateProperties(_deviceHandle));
    }

    public Task<CameraProperty?> GetPropertyAsync(string propertyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();
        return Task.FromResult(_nativeApi.GetProperty(_deviceHandle, propertyId));
    }

    public async Task<CameraPropertyUpdateResult> SetPropertyAsync(
        string propertyId,
        object value,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            var property = _nativeApi.GetProperty(_deviceHandle, propertyId);
            if (property is null)
            {
                return CameraPropertyUpdateResult.Failed($"Camera property '{propertyId}' was not found.");
            }

            var validation = CameraPropertyValidator.ValidateWrite(property, value, State is CameraConnectionState.Streaming);
            if (!validation.Success || validation.UpdatedProperty is null)
            {
                return validation;
            }

            try
            {
                var updated = _nativeApi.SetProperty(_deviceHandle, property, validation.UpdatedProperty.Value);
                RefreshSettingsAfterPropertyUpdate(updated);
                return CameraPropertyUpdateResult.Updated(updated);
            }
            catch (Exception exception) when (exception is DcamException or InvalidOperationException)
            {
                return CameraPropertyUpdateResult.Failed(exception.Message);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseDevice();
        _operationLock.Dispose();
        if (_runtime is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void RefreshSettingsAfterPropertyUpdate(CameraProperty property)
    {
        if (property.Id == "exposure.time")
        {
            _settings = _settings with { Exposure = TimeSpan.FromMilliseconds(Convert.ToDouble(property.Value)) };
            _exposureRange = DcamPropertyMapper.MapExposureRange(property);
            return;
        }

        _settings = _nativeApi.GetCaptureSettings(_deviceHandle);
        _exposureRange = _nativeApi.GetExposureRange(_deviceHandle);
    }

    private TimeSpan CalculateTimeout()
    {
        return _settings.Exposure + _options.FrameTimeout;
    }

    private void CloseDevice()
    {
        if (_deviceHandle != IntPtr.Zero)
        {
            _nativeApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
        }

        _connectedInfo = null;
        ConnectedDevice = null;
    }

    private void EnsureConnected()
    {
        if (_deviceHandle == IntPtr.Zero || ConnectedDevice is null || _connectedInfo is null)
        {
            throw new InvalidOperationException("No DCAM camera is connected.");
        }
    }

    private void SetState(CameraConnectionState state)
    {
        State = state;
    }
}
