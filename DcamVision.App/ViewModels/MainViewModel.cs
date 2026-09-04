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
    private readonly LiveAcquisitionService _liveAcquisitionService;
    private readonly ExposureSweepRunner _sweepRunner;
    private readonly ILogger<MainViewModel> _logger;
    private readonly ExposureSliderMapper _sliderMapper;
    private CameraDevice? _selectedDevice;
    private ImageSource? _previewImage;
    private string _statusMessage = "Ready.";
    private string _exposureValue;
    private string _exposureValidationMessage = string.Empty;
    private ExposureUnit _selectedExposureUnit = ExposureUnit.Milliseconds;
    private double _exposureSliderValue;
    private TimeSpan _pendingExposure;
    private bool _isSynchronizingExposure;
    private FrameStatistics? _statistics;
    private CameraFrame? _lastFrame;
    private int[] _histogramBins = [];
    private CancellationTokenSource? _sweepCancellation;
    private ApplicationOperation _operation = ApplicationOperation.Idle;
    private bool _isAutoContrastEnabled;
    private string _sweepStartValue = "1.000";
    private string _sweepEndValue = "100.000";
    private string _sweepStepValue = "10.000";
    private ExposureUnit _sweepStartUnit = ExposureUnit.Milliseconds;
    private ExposureUnit _sweepEndUnit = ExposureUnit.Milliseconds;
    private ExposureUnit _sweepStepUnit = ExposureUnit.Milliseconds;
    private string _framesPerExposure = "1";
    private string _delayBetweenCapturesMilliseconds = "0";
    private string _sweepValidationMessage = string.Empty;
    private string _sweepPreviewText = "11 exposure points | 11 total frames | Estimated duration: 0.0 s";
    private int _sweepExposureIndex;
    private int _sweepExposureCount;
    private int _sweepCapturedFrames;
    private int _sweepTotalFrames;
    private TimeSpan _sweepCurrentExposure;
    private SweepResultFrameViewModel? _selectedSweepResult;
    private LiveAcquisitionMetrics _liveMetrics;

    public MainViewModel(
        ICameraService cameraService,
        ImagePreviewService imagePreviewService,
        LiveAcquisitionService liveAcquisitionService,
        ExposureSweepRunner sweepRunner,
        ILogger<MainViewModel> logger)
    {
        _cameraService = cameraService;
        _imagePreviewService = imagePreviewService;
        _liveAcquisitionService = liveAcquisitionService;
        _sweepRunner = sweepRunner;
        _logger = logger;
        _sliderMapper = new ExposureSliderMapper(_cameraService.ExposureRange);
        _liveMetrics = _liveAcquisitionService.Metrics;
        _pendingExposure = _cameraService.CurrentSettings.Exposure;
        _exposureValue = FormatExposureValue(_pendingExposure, _selectedExposureUnit);
        _exposureSliderValue = _sliderMapper.ToSliderValue(_pendingExposure);

        foreach (var preset in ExposurePreset.Defaults)
        {
            ExposurePresets.Add(new ExposurePresetViewModel(preset, _cameraService.ExposureRange));
        }

        DiscoverCommand = new AsyncRelayCommand(DiscoverAsync, () => CanRunNormalOperation);
        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync, () => CanToggleConnection);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => CanRunNormalOperation && IsConnected);
        StartLiveCommand = new AsyncRelayCommand(StartLiveAsync, () => CanRunNormalOperation && IsConnected);
        PausePreviewCommand = new RelayCommand(PausePreview, () => IsLiveRunning);
        ResumePreviewCommand = new RelayCommand(ResumePreview, () => IsPreviewPaused);
        StopLiveCommand = new AsyncRelayCommand(StopLiveAsync, () => IsLive);
        ApplyExposureCommand = new AsyncRelayCommand(ApplyExposureFromEditorAsync, () => CanRunNormalOperation);
        ApplyPresetCommand = new AsyncParameterRelayCommand(ApplyPresetAsync, parameter => CanRunNormalOperation && parameter is ExposurePresetViewModel { IsSupported: true });
        RunSweepCommand = new AsyncRelayCommand(RunSweepAsync, () => CanRunNormalOperation && IsConnected);
        CancelSweepCommand = new RelayCommand(CancelSweep, () => IsSweepRunning);
        PreviousSweepResultCommand = new RelayCommand(SelectPreviousSweepResult, () => SelectedSweepResult?.Index > 0);
        NextSweepResultCommand = new RelayCommand(SelectNextSweepResult, () => SelectedSweepResult is not null && SelectedSweepResult.Index < SweepResults.Count - 1);

        _liveAcquisitionService.FrameReady += OnLiveFrameReady;
        _liveAcquisitionService.MetricsUpdated += OnLiveMetricsUpdated;
        _liveAcquisitionService.StateChanged += OnLiveStateChanged;
        _liveAcquisitionService.Faulted += OnLiveFaulted;

        RefreshPresetStates();
    }

    public ObservableCollection<CameraDevice> Devices { get; } = [];

    public ObservableCollection<CameraPropertySummary> PropertySummaries { get; } = [];

    public ObservableCollection<ExposurePresetViewModel> ExposurePresets { get; } = [];

    public ObservableCollection<SweepResultFrameViewModel> SweepResults { get; } = [];

    public IReadOnlyList<ExposureUnit> ExposureUnits { get; } =
    [
        ExposureUnit.Microseconds,
        ExposureUnit.Milliseconds,
        ExposureUnit.Seconds
    ];

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
                OnPropertyChanged(nameof(NoPreviewImage));
            }
        }
    }

    public bool NoPreviewImage => PreviewImage is null;

    public bool NoDevices => Devices.Count == 0;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ExposureValue
    {
        get => _exposureValue;
        set
        {
            if (SetProperty(ref _exposureValue, value) && !_isSynchronizingExposure)
            {
                ExposureValidationMessage = string.Empty;
                if (ExposureUnitConverter.TryFromDisplayValue(value, SelectedExposureUnit, out var parsed, out _))
                {
                    SetPendingExposure(parsed, updateEditor: false, updateSlider: true);
                }
            }
        }
    }

    public ExposureUnit SelectedExposureUnit
    {
        get => _selectedExposureUnit;
        set
        {
            if (SetProperty(ref _selectedExposureUnit, value))
            {
                UpdateExposureEditorFromPending();
            }
        }
    }

    public double ExposureSliderValue
    {
        get => _exposureSliderValue;
        set
        {
            if (SetProperty(ref _exposureSliderValue, value) && !_isSynchronizingExposure)
            {
                SetPendingExposure(_sliderMapper.FromSliderValue(value), updateEditor: true, updateSlider: false);
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

    public string SupportedExposureRangeText => $"{ExposureUnitConverter.Format(_cameraService.ExposureRange.Minimum)} to {ExposureUnitConverter.Format(_cameraService.ExposureRange.Maximum)}";

    public string CurrentExposureText => ExposureUnitConverter.Format(_cameraService.CurrentSettings.Exposure);

    public string PendingExposureText => ExposureUnitConverter.Format(_pendingExposure);

    public ApplicationOperation Operation
    {
        get => _operation;
        private set
        {
            if (SetProperty(ref _operation, value))
            {
                RefreshOperationState();
            }
        }
    }

    public bool IsBusy => Operation is ApplicationOperation.Busy;

    public bool IsSweepRunning => Operation is ApplicationOperation.Sweep;

    public bool IsLive => _liveAcquisitionService.State is LiveAcquisitionState.Running or LiveAcquisitionState.PreviewPaused or LiveAcquisitionState.Starting or LiveAcquisitionState.Stopping;

    public bool IsLiveRunning => _liveAcquisitionService.State is LiveAcquisitionState.Running;

    public bool IsPreviewPaused => _liveAcquisitionService.State is LiveAcquisitionState.PreviewPaused;

    public Visibility BusyVisibility => Operation is ApplicationOperation.Busy or ApplicationOperation.Sweep ? Visibility.Visible : Visibility.Collapsed;

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

    public string HeaderStatusText => Operation is ApplicationOperation.Sweep
        ? "Exposure Sweep"
        : IsLive
            ? LiveStateText
            : CameraStatusFormatter.FormatHeaderStatus(_cameraService.State);

    public string LiveStateText => _liveAcquisitionService.State switch
    {
        LiveAcquisitionState.PreviewPaused => "LIVE - PREVIEW PAUSED",
        LiveAcquisitionState.Running => "LIVE",
        LiveAcquisitionState.Starting => "STARTING",
        LiveAcquisitionState.Stopping => "STOPPING",
        LiveAcquisitionState.Faulted => "FAULTED",
        _ => "STOPPED"
    };

    public string HeaderDeviceText => CameraStatusFormatter.FormatDeviceName(_cameraService.ConnectedDevice);

    public string HeaderSimulatorText => SelectedDevice?.IsSimulated == true ? "SIMULATED CAMERA" : string.Empty;

    public Brush StatusIndicatorBrush => Operation switch
    {
        ApplicationOperation.Sweep => new SolidColorBrush(Color.FromRgb(217, 180, 95)),
        ApplicationOperation.Live => new SolidColorBrush(Color.FromRgb(93, 179, 199)),
        _ => _cameraService.State switch
        {
            CameraConnectionState.Connected => new SolidColorBrush(Color.FromRgb(108, 194, 143)),
            CameraConnectionState.Discovering or CameraConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(217, 180, 95)),
            _ => new SolidColorBrush(Color.FromRgb(116, 129, 144))
        }
    };

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

    public bool IsConnected => _cameraService.ConnectedDevice is not null;

    public bool IsDisconnected => !IsConnected;

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

    public string FrameExposureText => _lastFrame is null ? "-" : ExposureUnitConverter.Format(_lastFrame.Exposure);

    public string FrameTimestampText => _lastFrame is null ? "-" : _lastFrame.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public string MinimumPixelText => _statistics is null ? "-" : _statistics.Minimum.ToString(CultureInfo.InvariantCulture);

    public string MaximumPixelText => _statistics is null ? "-" : _statistics.Maximum.ToString(CultureInfo.InvariantCulture);

    public string MeanPixelText => _statistics is null ? "-" : _statistics.Mean.ToString("0.0", CultureInfo.InvariantCulture);

    public string SaturatedPixelText => _statistics is null ? "-" : _statistics.SaturatedPixelCount.ToString(CultureInfo.InvariantCulture);

    public string SaturationText => _statistics is null ? "-" : $"{_statistics.SaturationPercentage:0.0}%";

    public int[] HistogramBins
    {
        get => _histogramBins;
        private set => SetProperty(ref _histogramBins, value);
    }

    public string AcquisitionFpsText => _liveMetrics.AcquisitionFps.ToString("0.0", CultureInfo.InvariantCulture);

    public string DisplayFpsText => _liveMetrics.DisplayFps.ToString("0.0", CultureInfo.InvariantCulture);

    public string FramesAcquiredText => _liveMetrics.FramesAcquired.ToString("N0", CultureInfo.InvariantCulture);

    public string FramesProcessedText => _liveMetrics.FramesProcessed.ToString("N0", CultureInfo.InvariantCulture);

    public string FramesDisplayedText => _liveMetrics.FramesDisplayed.ToString("N0", CultureInfo.InvariantCulture);

    public string PipelineDropsText => _liveMetrics.PipelineDrops.ToString("N0", CultureInfo.InvariantCulture);

    public string SourceFrameGapsText => _liveMetrics.SourceFrameGaps.ToString("N0", CultureInfo.InvariantCulture);

    public string TotalDroppedText => _liveMetrics.TotalDropped.ToString("N0", CultureInfo.InvariantCulture);

    public string BufferOccupancyText => $"{_liveMetrics.BufferOccupancy} / {_liveMetrics.BufferCapacity}";

    public string SessionElapsedText => _liveMetrics.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    public string SweepStartValue
    {
        get => _sweepStartValue;
        set
        {
            if (SetProperty(ref _sweepStartValue, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public string SweepEndValue
    {
        get => _sweepEndValue;
        set
        {
            if (SetProperty(ref _sweepEndValue, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public string SweepStepValue
    {
        get => _sweepStepValue;
        set
        {
            if (SetProperty(ref _sweepStepValue, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public ExposureUnit SweepStartUnit
    {
        get => _sweepStartUnit;
        set
        {
            if (SetProperty(ref _sweepStartUnit, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public ExposureUnit SweepEndUnit
    {
        get => _sweepEndUnit;
        set
        {
            if (SetProperty(ref _sweepEndUnit, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public ExposureUnit SweepStepUnit
    {
        get => _sweepStepUnit;
        set
        {
            if (SetProperty(ref _sweepStepUnit, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public string FramesPerExposure
    {
        get => _framesPerExposure;
        set
        {
            if (SetProperty(ref _framesPerExposure, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public string DelayBetweenCapturesMilliseconds
    {
        get => _delayBetweenCapturesMilliseconds;
        set
        {
            if (SetProperty(ref _delayBetweenCapturesMilliseconds, value))
            {
                RefreshSweepPreview();
            }
        }
    }

    public string SweepValidationMessage
    {
        get => _sweepValidationMessage;
        private set
        {
            if (SetProperty(ref _sweepValidationMessage, value))
            {
                OnPropertyChanged(nameof(HasSweepValidationMessage));
            }
        }
    }

    public bool HasSweepValidationMessage => !string.IsNullOrWhiteSpace(SweepValidationMessage);

    public string SweepPreviewText
    {
        get => _sweepPreviewText;
        private set => SetProperty(ref _sweepPreviewText, value);
    }

    public string SweepProgressText => IsSweepRunning
        ? $"{_sweepExposureIndex} / {_sweepExposureCount} exposure points | Captured {_sweepCapturedFrames} / {_sweepTotalFrames} frames"
        : "No sweep running.";

    public string SweepCurrentExposureText => _sweepCurrentExposure == TimeSpan.Zero ? "-" : ExposureUnitConverter.Format(_sweepCurrentExposure);

    public double SweepProgressValue => _sweepTotalFrames == 0 ? 0 : _sweepCapturedFrames * 100.0 / _sweepTotalFrames;

    public bool HasSweepResults => SweepResults.Count > 0;

    public SweepResultFrameViewModel? SelectedSweepResult
    {
        get => _selectedSweepResult;
        set
        {
            if (SetProperty(ref _selectedSweepResult, value))
            {
                if (value is not null)
                {
                    PresentFrame(value.Result.Frame);
                }

                RefreshSelectedSweepResult();
                RaiseCommandStates();
            }
        }
    }

    public string SelectedSweepExposureText => SelectedSweepResult?.ExposureText ?? "-";

    public string SelectedSweepFrameText => SelectedSweepResult is null ? "-" : $"#{SelectedSweepResult.FrameNumberText}";

    public string SelectedSweepMeanText => SelectedSweepResult?.MeanText ?? "-";

    public string SelectedSweepMinimumText => SelectedSweepResult?.MinimumText ?? "-";

    public string SelectedSweepMaximumText => SelectedSweepResult?.MaximumText ?? "-";

    public string SelectedSweepMinMaxText => SelectedSweepResult is null ? "-" : $"{SelectedSweepResult.MinimumText} / {SelectedSweepResult.MaximumText}";

    public string SelectedSweepSaturationText => SelectedSweepResult?.SaturationText ?? "-";

    public string SelectedSweepSaturationState => SelectedSweepResult?.SaturationState ?? "-";

    public AsyncRelayCommand DiscoverCommand { get; }

    public AsyncRelayCommand ToggleConnectionCommand { get; }

    public AsyncRelayCommand CaptureCommand { get; }

    public AsyncRelayCommand StartLiveCommand { get; }

    public RelayCommand PausePreviewCommand { get; }

    public RelayCommand ResumePreviewCommand { get; }

    public AsyncRelayCommand StopLiveCommand { get; }

    public AsyncRelayCommand ApplyExposureCommand { get; }

    public AsyncParameterRelayCommand ApplyPresetCommand { get; }

    public AsyncRelayCommand RunSweepCommand { get; }

    public RelayCommand CancelSweepCommand { get; }

    public RelayCommand PreviousSweepResultCommand { get; }

    public RelayCommand NextSweepResultCommand { get; }

    private bool CanRunNormalOperation => Operation is ApplicationOperation.Idle && !IsLive;

    private bool CanToggleConnection => Operation is ApplicationOperation.Idle && (IsConnected || SelectedDevice is not null);

    private async Task ApplyExposureFromEditorAsync()
    {
        if (!_cameraService.ExposureRange.TryParse(ExposureValue, SelectedExposureUnit, out var exposure, out var errorMessage))
        {
            ExposureValidationMessage = errorMessage;
            StatusMessage = errorMessage;
            return;
        }

        SetPendingExposure(exposure, updateEditor: false, updateSlider: true);
        await ApplyExposureAsync(exposure);
    }

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
                if (IsLive)
                {
                    await _liveAcquisitionService.StopAsync();
                }

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
                SyncPendingExposure(_cameraService.CurrentSettings.Exposure);
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

    private async Task StartLiveAsync()
    {
        if (!IsConnected || !CanRunNormalOperation)
        {
            return;
        }

        await _liveAcquisitionService.StartAsync();
        StatusMessage = "Live acquisition started.";
    }

    private void PausePreview()
    {
        _liveAcquisitionService.PausePreview();
        StatusMessage = "Live preview paused. Acquisition continues.";
        RefreshOperationState();
    }

    private void ResumePreview()
    {
        _liveAcquisitionService.ResumePreview();
        StatusMessage = "Live preview resumed.";
        RefreshOperationState();
    }

    private async Task StopLiveAsync()
    {
        await _liveAcquisitionService.StopAsync();
        StatusMessage = "Live acquisition stopped.";
        RefreshOperationState();
    }

    private async Task ApplyPresetAsync(object? parameter)
    {
        if (parameter is not ExposurePresetViewModel preset || !preset.IsSupported)
        {
            return;
        }

        SetPendingExposure(preset.Exposure, updateEditor: true, updateSlider: true);
        await ApplyExposureAsync(preset.Exposure);
    }

    private async Task ApplyExposureAsync(TimeSpan exposure)
    {
        if (!_cameraService.ExposureRange.TryValidate(exposure, out var errorMessage))
        {
            ExposureValidationMessage = errorMessage;
            StatusMessage = errorMessage;
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            await _cameraService.SetExposureAsync(exposure);
            SyncPendingExposure(exposure);
            await RefreshPropertiesAsync();
            StatusMessage = $"Exposure updated to {ExposureUnitConverter.Format(exposure)}.";
        });
    }

    private async Task RunSweepAsync()
    {
        if (!TryCreateSweepSettings(out var settings))
        {
            StatusMessage = SweepValidationMessage;
            return;
        }

        _sweepCancellation = new CancellationTokenSource();
        Operation = ApplicationOperation.Sweep;
        ClearSweepProgress();
        SweepResults.Clear();
        OnPropertyChanged(nameof(HasSweepResults));
        OnPropertyChanged(nameof(SweepResults));
        StatusMessage = "Exposure sweep started.";

        var progress = new Progress<ExposureSweepProgress>(UpdateSweepProgress);

        try
        {
            var result = await _sweepRunner.RunAsync(settings, progress, _sweepCancellation.Token);
            SweepResults.Clear();
            for (var i = 0; i < result.Frames.Count; i++)
            {
                SweepResults.Add(new SweepResultFrameViewModel(i, result.Frames[i]));
            }

            OnPropertyChanged(nameof(SweepResults));
            OnPropertyChanged(nameof(HasSweepResults));
            SelectedSweepResult = SweepResults.FirstOrDefault();
            StatusMessage = $"Exposure sweep complete: {result.Frames.Count} frame(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Exposure sweep cancelled.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exposure sweep failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            _sweepCancellation?.Dispose();
            _sweepCancellation = null;
            Operation = ApplicationOperation.Idle;
            SyncPendingExposure(_cameraService.CurrentSettings.Exposure);
            await RefreshPropertiesAsync();
            OnPropertyChanged(nameof(HasSweepResults));
        }
    }

    private void CancelSweep()
    {
        if (!IsSweepRunning || _sweepCancellation is null)
        {
            return;
        }

        _sweepCancellation.Cancel();
        StatusMessage = "Cancelling exposure sweep...";
    }

    private bool TryCreateSweepSettings(out ExposureSweepSettings settings)
    {
        settings = new ExposureSweepSettings
        {
            Start = TimeSpan.Zero,
            End = TimeSpan.Zero,
            Step = TimeSpan.Zero
        };

        if (!_cameraService.ExposureRange.TryParse(SweepStartValue, SweepStartUnit, out var start, out var errorMessage))
        {
            SweepValidationMessage = $"Start exposure: {errorMessage}";
            return false;
        }

        if (!_cameraService.ExposureRange.TryParse(SweepEndValue, SweepEndUnit, out var end, out errorMessage))
        {
            SweepValidationMessage = $"End exposure: {errorMessage}";
            return false;
        }

        if (!ExposureUnitConverter.TryFromDisplayValue(SweepStepValue, SweepStepUnit, out var step, out errorMessage))
        {
            SweepValidationMessage = $"Step exposure: {errorMessage}";
            return false;
        }

        if (!int.TryParse(FramesPerExposure, NumberStyles.Integer, CultureInfo.InvariantCulture, out var framesPerExposure))
        {
            SweepValidationMessage = "Frames per exposure must be a whole number.";
            return false;
        }

        if (!double.TryParse(DelayBetweenCapturesMilliseconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var delayMilliseconds) || delayMilliseconds < 0)
        {
            SweepValidationMessage = "Delay between captures must be zero or greater.";
            return false;
        }

        settings = new ExposureSweepSettings
        {
            Start = start,
            End = end,
            Step = step,
            FramesPerExposure = framesPerExposure,
            DelayBetweenCaptures = TimeSpan.FromMilliseconds(delayMilliseconds),
            ExposureRange = _cameraService.ExposureRange
        };

        try
        {
            ExposureSweepGenerator.Validate(settings);
            SweepValidationMessage = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            SweepValidationMessage = exception.Message;
            return false;
        }
    }

    private void RefreshSweepPreview()
    {
        if (!TryCreateSweepSettings(out var settings))
        {
            SweepPreviewText = "Fix sweep settings to preview the sequence.";
            return;
        }

        try
        {
            var exposures = ExposureSweepGenerator.Generate(settings);
            var totalFrames = exposures.Count * settings.FramesPerExposure;
            var estimatedSeconds = totalFrames * 0.07 + Math.Max(0, totalFrames - 1) * settings.DelayBetweenCaptures.TotalSeconds;
            SweepPreviewText = $"{exposures.Count} exposure points | {totalFrames} total frames | Estimated duration: {estimatedSeconds:0.0} s";
        }
        catch (Exception exception)
        {
            SweepValidationMessage = exception.Message;
            SweepPreviewText = "Fix sweep settings to preview the sequence.";
        }
    }

    private void UpdateSweepProgress(ExposureSweepProgress progress)
    {
        _sweepExposureIndex = progress.ExposureIndex;
        _sweepExposureCount = progress.ExposureCount;
        _sweepCapturedFrames = progress.CapturedFrames;
        _sweepTotalFrames = progress.TotalFrames;
        _sweepCurrentExposure = progress.CurrentExposure;
        OnPropertyChanged(nameof(SweepProgressText));
        OnPropertyChanged(nameof(SweepCurrentExposureText));
        OnPropertyChanged(nameof(SweepProgressValue));
        StatusMessage = $"Exposure sweep: {SweepProgressValue:0}% complete.";
    }

    private void SelectPreviousSweepResult()
    {
        if (SelectedSweepResult?.Index > 0)
        {
            SelectedSweepResult = SweepResults[SelectedSweepResult.Index - 1];
        }
    }

    private void SelectNextSweepResult()
    {
        if (SelectedSweepResult is not null && SelectedSweepResult.Index < SweepResults.Count - 1)
        {
            SelectedSweepResult = SweepResults[SelectedSweepResult.Index + 1];
        }
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

    private void PresentFrame(ProcessedFrame processedFrame)
    {
        _lastFrame = processedFrame.Frame;
        _statistics = processedFrame.Statistics;
        HistogramBins = processedFrame.Histogram;
        RefreshPreviewImage();
        RefreshFrameReadouts();
    }

    private void RefreshPreviewImage()
    {
        PreviewImage = _lastFrame is null
            ? null
            : _imagePreviewService.CreatePreview(_lastFrame, IsAutoContrastEnabled);
    }

    private async Task RunBusyOperationAsync(Func<Task> operation)
    {
        if (Operation is not ApplicationOperation.Idle)
        {
            return;
        }

        try
        {
            Operation = ApplicationOperation.Busy;
            await operation();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Camera workflow operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            Operation = ApplicationOperation.Idle;
        }
    }

    private void SyncPendingExposure(TimeSpan exposure)
    {
        SetPendingExposure(exposure, updateEditor: true, updateSlider: true);
        ExposureValidationMessage = string.Empty;
        OnPropertyChanged(nameof(CurrentExposureText));
        RefreshPresetStates();
    }

    private void SetPendingExposure(TimeSpan exposure, bool updateEditor, bool updateSlider)
    {
        _pendingExposure = exposure;
        OnPropertyChanged(nameof(PendingExposureText));
        _isSynchronizingExposure = true;
        try
        {
            if (updateEditor)
            {
                ExposureValue = FormatExposureValue(exposure, SelectedExposureUnit);
            }

            if (updateSlider)
            {
                ExposureSliderValue = _sliderMapper.ToSliderValue(exposure);
            }
        }
        finally
        {
            _isSynchronizingExposure = false;
        }

        RefreshPresetStates();
        RaiseCommandStates();
    }

    private void UpdateExposureEditorFromPending()
    {
        _isSynchronizingExposure = true;
        try
        {
            ExposureValue = FormatExposureValue(_pendingExposure, SelectedExposureUnit);
        }
        finally
        {
            _isSynchronizingExposure = false;
        }
    }

    private void RefreshPresetStates()
    {
        foreach (var preset in ExposurePresets)
        {
            preset.IsSupported = _cameraService.ExposureRange.Contains(preset.Exposure);
            preset.IsActive = preset.Exposure == _cameraService.CurrentSettings.Exposure;
        }
    }

    private void ClearSweepProgress()
    {
        _sweepExposureIndex = 0;
        _sweepExposureCount = 0;
        _sweepCapturedFrames = 0;
        _sweepTotalFrames = 0;
        _sweepCurrentExposure = TimeSpan.Zero;
        OnPropertyChanged(nameof(SweepProgressText));
        OnPropertyChanged(nameof(SweepCurrentExposureText));
        OnPropertyChanged(nameof(SweepProgressValue));
    }

    private static string FormatExposureValue(TimeSpan exposure, ExposureUnit unit)
    {
        return ExposureUnitConverter.ToDisplayValue(exposure, unit).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void RefreshOperationState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsSweepRunning));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(IsLiveRunning));
        OnPropertyChanged(nameof(IsPreviewPaused));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(LiveStateText));
        OnPropertyChanged(nameof(HeaderStatusText));
        OnPropertyChanged(nameof(HeaderDeviceText));
        OnPropertyChanged(nameof(HeaderSimulatorText));
        OnPropertyChanged(nameof(StatusIndicatorBrush));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(SelectedConnectionState));
        OnPropertyChanged(nameof(SweepProgressText));
        RaiseCommandStates();
    }

    private void OnLiveFrameReady(object? sender, ProcessedFrame frame)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            PresentFrame(frame);
            StatusMessage = IsPreviewPaused ? "Live preview paused. Acquisition continues." : $"Live frame #{FrameNumberText}.";
            RefreshOperationState();
        });
    }

    private void OnLiveMetricsUpdated(object? sender, LiveAcquisitionMetrics metrics)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _liveMetrics = metrics;
            RefreshLiveMetrics();
        });
    }

    private void OnLiveStateChanged(object? sender, LiveAcquisitionState state)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(RefreshOperationState);
    }

    private void OnLiveFaulted(object? sender, Exception exception)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusMessage = $"Live acquisition failed: {exception.Message}";
            RefreshOperationState();
        });
    }

    private void RefreshLiveMetrics()
    {
        OnPropertyChanged(nameof(AcquisitionFpsText));
        OnPropertyChanged(nameof(DisplayFpsText));
        OnPropertyChanged(nameof(FramesAcquiredText));
        OnPropertyChanged(nameof(FramesProcessedText));
        OnPropertyChanged(nameof(FramesDisplayedText));
        OnPropertyChanged(nameof(PipelineDropsText));
        OnPropertyChanged(nameof(SourceFrameGapsText));
        OnPropertyChanged(nameof(TotalDroppedText));
        OnPropertyChanged(nameof(BufferOccupancyText));
        OnPropertyChanged(nameof(SessionElapsedText));
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
        OnPropertyChanged(nameof(FrameTimestampText));
        OnPropertyChanged(nameof(MinimumPixelText));
        OnPropertyChanged(nameof(MaximumPixelText));
        OnPropertyChanged(nameof(MeanPixelText));
        OnPropertyChanged(nameof(SaturatedPixelText));
        OnPropertyChanged(nameof(SaturationText));
    }

    private void RefreshSelectedSweepResult()
    {
        OnPropertyChanged(nameof(SelectedSweepExposureText));
        OnPropertyChanged(nameof(SelectedSweepFrameText));
        OnPropertyChanged(nameof(SelectedSweepMeanText));
        OnPropertyChanged(nameof(SelectedSweepMinimumText));
        OnPropertyChanged(nameof(SelectedSweepMaximumText));
        OnPropertyChanged(nameof(SelectedSweepMinMaxText));
        OnPropertyChanged(nameof(SelectedSweepSaturationText));
        OnPropertyChanged(nameof(SelectedSweepSaturationState));
    }

    private void NotifyDeviceCollectionChanged()
    {
        OnPropertyChanged(nameof(NoDevices));
    }

    private void RaiseCommandStates()
    {
        DiscoverCommand.RaiseCanExecuteChanged();
        ToggleConnectionCommand.RaiseCanExecuteChanged();
        CaptureCommand.RaiseCanExecuteChanged();
        StartLiveCommand.RaiseCanExecuteChanged();
        PausePreviewCommand.RaiseCanExecuteChanged();
        ResumePreviewCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        ApplyExposureCommand.RaiseCanExecuteChanged();
        ApplyPresetCommand.RaiseCanExecuteChanged();
        RunSweepCommand.RaiseCanExecuteChanged();
        CancelSweepCommand.RaiseCanExecuteChanged();
        PreviousSweepResultCommand.RaiseCanExecuteChanged();
        NextSweepResultCommand.RaiseCanExecuteChanged();
    }
}
