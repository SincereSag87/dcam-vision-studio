using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DcamVision.App.Services;
using DcamVision.Core;
using DcamVision.Imaging;
using Microsoft.Extensions.Logging;

namespace DcamVision.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICameraService _cameraService;
    private readonly ImagePreviewService _imagePreviewService;
    private readonly ILogger<MainViewModel> _logger;
    private CameraDevice? _selectedDevice;
    private ImageSource? _previewImage;
    private string _statusMessage = "Ready.";
    private string _exposureMilliseconds;
    private string _exposureValidationMessage = string.Empty;
    private FrameStatistics? _statistics;
    private CameraFrame? _lastFrame;
    private int[] _histogramBins = [];
    private CancellationTokenSource? _liveCancellation;
    private bool _isBusy;
    private bool _isAutoContrastEnabled;

    public MainViewModel(
        ICameraService cameraService,
        ImagePreviewService imagePreviewService,
        ILogger<MainViewModel> logger)
    {
        _cameraService = cameraService;
        _imagePreviewService = imagePreviewService;
        _logger = logger;
        _exposureMilliseconds = _cameraService.CurrentSettings.Exposure.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture);

        DiscoverCommand = new AsyncRelayCommand(DiscoverAsync, () => !IsBusy && !IsLive);
        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync, () => !IsBusy && (SelectedDevice is not null || IsConnected));
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => !IsBusy && IsConnected && !IsLive);
        StartLiveCommand = new AsyncRelayCommand(StartLiveAsync, () => !IsBusy && IsConnected && !IsLive);
        StopLiveCommand = new RelayCommand(StopLive, () => IsLive);
        ApplyExposureCommand = new AsyncRelayCommand(ApplyExposureAsync, () => !IsBusy);
    }

    public ObservableCollection<CameraDevice> Devices { get; } = [];

    public ObservableCollection<CameraPropertySummary> PropertySummaries { get; } = [];

    public CameraDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                RefreshDeviceDetails();
                RaiseCommandStates();
            }
        }
    }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (SetProperty(ref _previewImage, value))
            {
                OnPropertyChanged(nameof(HasPreviewImage));
                OnPropertyChanged(nameof(NoPreviewImage));
            }
        }
    }

    public bool HasPreviewImage => PreviewImage is not null;

    public bool NoPreviewImage => PreviewImage is null;

    public bool HasDevices => Devices.Count > 0;

    public bool NoDevices => Devices.Count == 0;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ExposureMilliseconds
    {
        get => _exposureMilliseconds;
        set
        {
            if (SetProperty(ref _exposureMilliseconds, value))
            {
                ExposureValidationMessage = string.Empty;
            }
        }
    }

    public string ExposureValidationMessage
    {
        get => _exposureValidationMessage;
        private set
        {
            if (SetProperty(ref _exposureValidationMessage, value))
            {
                OnPropertyChanged(nameof(HasExposureValidationMessage));
            }
        }
    }

    public bool HasExposureValidationMessage => !string.IsNullOrWhiteSpace(ExposureValidationMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(BusyVisibility));
                RaiseCommandStates();
            }
        }
    }

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public bool IsAutoContrastEnabled
    {
        get => _isAutoContrastEnabled;
        set
        {
            if (SetProperty(ref _isAutoContrastEnabled, value) && _lastFrame is not null)
            {
                RefreshPreviewImage();
            }
        }
    }

    public string HeaderStatusText => CameraStatusFormatter.FormatHeaderStatus(_cameraService.State);

    public string HeaderDeviceText => CameraStatusFormatter.FormatDeviceName(_cameraService.ConnectedDevice);

    public string HeaderSimulatorText => SelectedDevice?.IsSimulated == true ? "SIMULATED CAMERA" : string.Empty;

    public Brush StatusIndicatorBrush => _cameraService.State switch
    {
        CameraConnectionState.Connected => new SolidColorBrush(Color.FromRgb(108, 194, 143)),
        CameraConnectionState.Streaming => new SolidColorBrush(Color.FromRgb(93, 179, 199)),
        CameraConnectionState.Discovering or CameraConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(217, 180, 95)),
        _ => new SolidColorBrush(Color.FromRgb(116, 129, 144))
    };

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

    public bool IsConnected => _cameraService.ConnectedDevice is not null;

    public bool IsDisconnected => !IsConnected;

    public bool IsLive => _liveCancellation is not null;

    public bool IsSelectedDeviceSimulated => SelectedDevice?.IsSimulated == true;

    public string SelectedDeviceName => SelectedDevice?.DisplayName ?? "No camera selected";

    public string SelectedManufacturer => SelectedDevice?.Manufacturer ?? "-";

    public string SelectedModel => SelectedDevice?.Model ?? "-";

    public string SelectedSerialNumber => SelectedDevice?.SerialNumber ?? "-";

    public string SelectedDeviceKind => SelectedDevice is null ? "-" : SelectedDevice.IsSimulated ? "SIMULATED" : "PHYSICAL";

    public string SelectedConnectionState => IsConnected ? HeaderStatusText : "Disconnected";

    public string FrameNumberText => _statistics is null ? "-" : _statistics.FrameNumber.ToString(CultureInfo.InvariantCulture);

    public string FrameDimensionsText => _statistics is null ? "-" : $"{_statistics.Width} x {_statistics.Height}";

    public string FramePixelFormatText => _lastFrame?.PixelFormat.ToString() ?? "-";

    public string FrameExposureText => _lastFrame is null ? "-" : $"{_lastFrame.Exposure.TotalMilliseconds:0.###} ms";

    public string CurrentExposureText => $"{_cameraService.CurrentSettings.Exposure.TotalMilliseconds:0.###} ms";

    public string FrameTimestampText => _lastFrame is null ? "-" : _lastFrame.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public string MinimumPixelText => _statistics is null ? "-" : _statistics.Minimum.ToString(CultureInfo.InvariantCulture);

    public string MaximumPixelText => _statistics is null ? "-" : _statistics.Maximum.ToString(CultureInfo.InvariantCulture);

    public string MeanPixelText => _statistics is null ? "-" : _statistics.Mean.ToString("0.0", CultureInfo.InvariantCulture);

    public int[] HistogramBins
    {
        get => _histogramBins;
        private set
        {
            if (SetProperty(ref _histogramBins, value))
            {
                OnPropertyChanged(nameof(HasHistogram));
            }
        }
    }

    public bool HasHistogram => HistogramBins.Length > 0;

    public AsyncRelayCommand DiscoverCommand { get; }

    public AsyncRelayCommand ToggleConnectionCommand { get; }

    public AsyncRelayCommand CaptureCommand { get; }

    public AsyncRelayCommand StartLiveCommand { get; }

    public RelayCommand StopLiveCommand { get; }

    public AsyncRelayCommand ApplyExposureCommand { get; }

    private async Task DiscoverAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            StatusMessage = "Discovering cameras...";
            Devices.Clear();
            NotifyDeviceCollectionChanged();

            var devices = await _cameraService.DiscoverAsync();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            SelectedDevice = Devices.FirstOrDefault();
            StatusMessage = Devices.Count == 0
                ? "No cameras discovered. Run discovery to find available devices."
                : $"Discovered {Devices.Count} camera(s).";
            NotifyDeviceCollectionChanged();
        });
    }

    private async Task ToggleConnectionAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            if (IsConnected)
            {
                StopLive();
                await _cameraService.DisconnectAsync();
                PropertySummaries.Clear();
                StatusMessage = "Camera disconnected.";
            }
            else
            {
                if (SelectedDevice is null)
                {
                    StatusMessage = "Select a camera before connecting.";
                    return;
                }

                StatusMessage = $"Connecting to {SelectedDevice.DisplayName}...";
                await _cameraService.ConnectAsync(SelectedDevice.Id);
                await RefreshPropertiesAsync();
                StatusMessage = $"Connected to {SelectedDevice.DisplayName}.";
            }
        });
    }

    private async Task CaptureAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            StatusMessage = "Capturing frame...";
            PresentFrame(await _cameraService.CaptureAsync());
            StatusMessage = $"Captured frame #{FrameNumberText}.";
        });
    }

    private Task StartLiveAsync()
    {
        if (!IsConnected || IsLive)
        {
            return Task.CompletedTask;
        }

        _liveCancellation = new CancellationTokenSource();
        RefreshState();
        StatusMessage = "Live acquisition started.";
        _ = StreamLiveFramesAsync(_liveCancellation, _liveCancellation.Token);
        return Task.CompletedTask;
    }

    private void StopLive()
    {
        if (_liveCancellation is null)
        {
            return;
        }

        _liveCancellation.Cancel();
        StatusMessage = "Live acquisition stopped.";
        RefreshState();
    }

    private async Task StreamLiveFramesAsync(CancellationTokenSource source, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _cameraService.StreamFramesAsync(cancellationToken))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PresentFrame(frame);
                    StatusMessage = $"Live frame #{FrameNumberText}.";
                    RefreshState();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Live acquisition failed.");
            await Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = exception.Message);
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(_liveCancellation, source))
                {
                    _liveCancellation.Dispose();
                    _liveCancellation = null;
                }

                RefreshState();
            });
        }
    }

    private async Task ApplyExposureAsync()
    {
        if (!CameraExposureRange.TryParseMilliseconds(ExposureMilliseconds, out var exposure, out var errorMessage))
        {
            ExposureValidationMessage = errorMessage;
            StatusMessage = errorMessage;
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            await _cameraService.SetExposureAsync(exposure);
            ExposureMilliseconds = exposure.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(CurrentExposureText));
            await RefreshPropertiesAsync();
            StatusMessage = $"Exposure updated to {exposure.TotalMilliseconds:0.###} ms.";
        });
    }

    private async Task RefreshPropertiesAsync()
    {
        var properties = await _cameraService.GetPropertiesAsync();
        PropertySummaries.Clear();

        AddPropertySummary(properties, "gain");
        AddPropertySummary(properties, "width");
        AddPropertySummary(properties, "height");
        AddPropertySummary(properties, "pixelFormat");
        AddPropertySummary(properties, "triggerMode");
    }

    private void AddPropertySummary(IReadOnlyList<CameraProperty> properties, string name)
    {
        var property = properties.FirstOrDefault(candidate => candidate.Name == name);
        if (property is null)
        {
            return;
        }

        var value = property.Unit is null ? property.Value.ToString() : $"{property.Value} {property.Unit}";
        PropertySummaries.Add(new CameraPropertySummary(property.DisplayName, value ?? string.Empty));
    }

    private void PresentFrame(CameraFrame frame)
    {
        _lastFrame = frame;
        _statistics = FrameStatisticsCalculator.Calculate(frame);
        HistogramBins = HistogramCalculator.Calculate16Bit(frame, bins: 256);
        RefreshPreviewImage();
        RefreshFrameReadouts();
    }

    private void RefreshPreviewImage()
    {
        if (_lastFrame is null)
        {
            PreviewImage = null;
            return;
        }

        PreviewImage = _imagePreviewService.CreatePreview(_lastFrame, IsAutoContrastEnabled);
    }

    private async Task RunBusyOperationAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            RefreshState();
            await operation();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Camera workflow operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            RefreshState();
        }
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(HeaderStatusText));
        OnPropertyChanged(nameof(HeaderDeviceText));
        OnPropertyChanged(nameof(HeaderSimulatorText));
        OnPropertyChanged(nameof(StatusIndicatorBrush));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(SelectedConnectionState));
        RaiseCommandStates();
    }

    private void RefreshDeviceDetails()
    {
        OnPropertyChanged(nameof(SelectedDeviceName));
        OnPropertyChanged(nameof(SelectedManufacturer));
        OnPropertyChanged(nameof(SelectedModel));
        OnPropertyChanged(nameof(SelectedSerialNumber));
        OnPropertyChanged(nameof(SelectedDeviceKind));
        OnPropertyChanged(nameof(IsSelectedDeviceSimulated));
        OnPropertyChanged(nameof(HeaderSimulatorText));
    }

    private void RefreshFrameReadouts()
    {
        OnPropertyChanged(nameof(FrameNumberText));
        OnPropertyChanged(nameof(FrameDimensionsText));
        OnPropertyChanged(nameof(FramePixelFormatText));
        OnPropertyChanged(nameof(FrameExposureText));
        OnPropertyChanged(nameof(CurrentExposureText));
        OnPropertyChanged(nameof(FrameTimestampText));
        OnPropertyChanged(nameof(MinimumPixelText));
        OnPropertyChanged(nameof(MaximumPixelText));
        OnPropertyChanged(nameof(MeanPixelText));
    }

    private void NotifyDeviceCollectionChanged()
    {
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(NoDevices));
    }

    private void RaiseCommandStates()
    {
        DiscoverCommand.RaiseCanExecuteChanged();
        ToggleConnectionCommand.RaiseCanExecuteChanged();
        CaptureCommand.RaiseCanExecuteChanged();
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        ApplyExposureCommand.RaiseCanExecuteChanged();
    }
}
