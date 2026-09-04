using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DcamVision.Core;
using DcamVision.Imaging;
using Microsoft.Extensions.Logging;

namespace DcamVision.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICameraService _cameraService;
    private readonly ILogger<MainViewModel> _logger;
    private CameraDevice? _selectedDevice;
    private ImageSource? _previewImage;
    private string _statusMessage = "Ready.";
    private string _latestFrameSummary = "No frame captured.";
    private string _exposureMilliseconds;
    private CancellationTokenSource? _liveCancellation;

    public MainViewModel(ICameraService cameraService, ILogger<MainViewModel> logger)
    {
        _cameraService = cameraService;
        _logger = logger;
        _exposureMilliseconds = _cameraService.CurrentSettings.Exposure.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);

        DiscoverCommand = new AsyncRelayCommand(DiscoverAsync);
        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync, () => SelectedDevice is not null || IsConnected);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => IsConnected);
        StartLiveCommand = new AsyncRelayCommand(StartLiveAsync, () => IsConnected && !IsLive);
        StopLiveCommand = new RelayCommand(StopLive, () => IsLive);
        ApplyExposureCommand = new AsyncRelayCommand(ApplyExposureAsync);
    }

    public ObservableCollection<CameraDevice> Devices { get; } = [];

    public ObservableCollection<CameraProperty> Properties { get; } = [];

    public CameraDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
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
                OnPropertyChanged(nameof(PreviewPlaceholder));
            }
        }
    }

    public string PreviewPlaceholder => PreviewImage is null ? "Image preview" : string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LatestFrameSummary
    {
        get => _latestFrameSummary;
        private set => SetProperty(ref _latestFrameSummary, value);
    }

    public string ExposureMilliseconds
    {
        get => _exposureMilliseconds;
        set => SetProperty(ref _exposureMilliseconds, value);
    }

    public string ConnectionStatus => _cameraService.State switch
    {
        CameraConnectionState.Disconnected => "Disconnected",
        CameraConnectionState.Discovering => "Discovering",
        CameraConnectionState.Connecting => "Connecting",
        CameraConnectionState.Connected => $"Connected: {_cameraService.ConnectedDevice?.DisplayName}",
        CameraConnectionState.Streaming => $"Live: {_cameraService.ConnectedDevice?.DisplayName}",
        _ => _cameraService.State.ToString()
    };

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

    public bool IsConnected => _cameraService.ConnectedDevice is not null;

    public bool IsLive => _liveCancellation is not null;

    public AsyncRelayCommand DiscoverCommand { get; }

    public AsyncRelayCommand ToggleConnectionCommand { get; }

    public AsyncRelayCommand CaptureCommand { get; }

    public AsyncRelayCommand StartLiveCommand { get; }

    public RelayCommand StopLiveCommand { get; }

    public AsyncRelayCommand ApplyExposureCommand { get; }

    private async Task DiscoverAsync()
    {
        await RunUiOperationAsync(async () =>
        {
            StatusMessage = "Discovering cameras...";
            Devices.Clear();

            var devices = await _cameraService.DiscoverAsync();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            SelectedDevice = Devices.FirstOrDefault();
            StatusMessage = Devices.Count == 0 ? "No cameras found." : $"Found {Devices.Count} camera(s).";
            RefreshState();
        });
    }

    private async Task ToggleConnectionAsync()
    {
        await RunUiOperationAsync(async () =>
        {
            if (IsConnected)
            {
                StopLive();
                await _cameraService.DisconnectAsync();
                Properties.Clear();
                PreviewImage = null;
                LatestFrameSummary = "No frame captured.";
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

            RefreshState();
        });
    }

    private async Task CaptureAsync()
    {
        await RunUiOperationAsync(async () =>
        {
            StatusMessage = "Capturing frame...";
            var frame = await _cameraService.CaptureAsync();
            PresentFrame(frame);
            StatusMessage = $"Captured frame {frame.FrameNumber}.";
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
                    StatusMessage = $"Live frame {frame.FrameNumber}.";
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
        await RunUiOperationAsync(async () =>
        {
            if (!double.TryParse(ExposureMilliseconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
            {
                StatusMessage = "Exposure must be a number in milliseconds.";
                return;
            }

            await _cameraService.SetExposureAsync(TimeSpan.FromMilliseconds(milliseconds));
            await RefreshPropertiesAsync();
            StatusMessage = $"Exposure set to {milliseconds:0.###} ms.";
        });
    }

    private async Task RefreshPropertiesAsync()
    {
        var properties = await _cameraService.GetPropertiesAsync();
        Properties.Clear();
        foreach (var property in properties)
        {
            Properties.Add(property);
        }
    }

    private void PresentFrame(CameraFrame frame)
    {
        var pixels = FrameConverter.ToGrayscale8(frame);
        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Gray8,
            null,
            pixels,
            frame.Width);
        bitmap.Freeze();

        PreviewImage = bitmap;
        LatestFrameSummary = $"{frame.Width} x {frame.Height} Mono16 | Frame {frame.FrameNumber} | {frame.Exposure.TotalMilliseconds:0.###} ms";
    }

    private async Task RunUiOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Camera workflow operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            RefreshState();
        }
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(ConnectionStatus));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsLive));
        RaiseCommandStates();
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
