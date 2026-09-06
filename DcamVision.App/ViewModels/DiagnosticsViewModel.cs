using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DcamVision.Imaging.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DcamVision.App.ViewModels;

public sealed class DiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IApplicationDiagnosticsService _diagnosticsService;
    private readonly SupportBundleService _supportBundleService;
    private readonly InMemoryDiagnosticLogStore _logStore;
    private readonly Func<DiagnosticsState> _stateFactory;
    private readonly DispatcherTimer _timer;
    private readonly string _logDirectory;
    private ApplicationDiagnosticsSnapshot? _snapshot;
    private LogLevel _minimumLogLevel = LogLevel.Information;
    private string _categoryFilter = string.Empty;
    private string _searchText = string.Empty;
    private bool _isPaused;
    private bool _isCreatingSupportBundle;
    private string _supportBundleStatus = "No support bundle created.";

    public DiagnosticsViewModel(
        IApplicationDiagnosticsService diagnosticsService,
        SupportBundleService supportBundleService,
        InMemoryDiagnosticLogStore logStore,
        string logDirectory,
        Func<DiagnosticsState> stateFactory)
    {
        _diagnosticsService = diagnosticsService;
        _supportBundleService = supportBundleService;
        _logStore = logStore;
        _logDirectory = logDirectory;
        _stateFactory = stateFactory;
        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics);
        ClearLogViewCommand = new RelayCommand(ClearLogView);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        CreateSupportBundleCommand = new AsyncRelayCommand(CreateSupportBundleAsync, () => !IsCreatingSupportBundle);
        _logStore.EntryAdded += OnLogEntryAdded;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    public ObservableCollection<DiagnosticLogEntry> LogEntries { get; } = [];

    public IReadOnlyList<LogLevel> LogLevels { get; } =
    [
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical
    ];

    public RelayCommand CopyDiagnosticsCommand { get; }

    public RelayCommand ClearLogViewCommand { get; }

    public RelayCommand OpenLogFolderCommand { get; }

    public AsyncRelayCommand CreateSupportBundleCommand { get; }

    public LogLevel MinimumLogLevel
    {
        get => _minimumLogLevel;
        set
        {
            if (SetProperty(ref _minimumLogLevel, value))
            {
                RefreshLogEntries();
            }
        }
    }

    public string CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            if (SetProperty(ref _categoryFilter, value))
            {
                RefreshLogEntries();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshLogEntries();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set => SetProperty(ref _isPaused, value);
    }

    public bool IsCreatingSupportBundle
    {
        get => _isCreatingSupportBundle;
        private set
        {
            if (SetProperty(ref _isCreatingSupportBundle, value))
            {
                CreateSupportBundleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SupportBundleStatus
    {
        get => _supportBundleStatus;
        private set => SetProperty(ref _supportBundleStatus, value);
    }

    public string HealthSummaryText => _snapshot is null ? "Unknown" : $"Diagnostics: {_snapshot.Health.Overall}";

    public string ApplicationText => _snapshot is null ? "-" : $"{_snapshot.Application.ApplicationName} {_snapshot.Application.ApplicationVersion}";

    public string RuntimeText => _snapshot is null ? "-" : $"{_snapshot.Runtime.DotNetVersion} | {_snapshot.Runtime.ProcessArchitecture}";

    public string UptimeText => _snapshot is null ? "-" : _snapshot.Application.Uptime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    public string MemoryText => _snapshot is null ? "-" : $"Working set {_snapshot.Runtime.WorkingSetBytes / 1024.0 / 1024.0:0.0} MB | Managed {_snapshot.Runtime.ManagedMemoryBytes / 1024.0 / 1024.0:0.0} MB";

    public string CameraText => _snapshot is null ? "-" : $"Backend {_snapshot.Camera.SelectedBackend} | Runtime {(_snapshot.Camera.DcamRuntimeAvailable ? "Available" : "Unavailable")} | State {_snapshot.Camera.State}";

    public string AcquisitionText => _snapshot is null ? "-" : $"{_snapshot.Acquisition.LiveState} | Acq {_snapshot.Acquisition.AcquisitionFps:0.0} FPS | Display {_snapshot.Acquisition.DisplayFps:0.0} FPS | Drops {_snapshot.Acquisition.PipelineDrops}";

    public string ImagingText => _snapshot is null ? "-" : $"Frame {_snapshot.Imaging.CurrentFrameNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"} | {_snapshot.Imaging.Resolution ?? "-"} | Processing {_snapshot.Imaging.ProcessingTime?.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"} ms";

    public string HistoryText => _snapshot is null ? "-" : $"{_snapshot.History.CaptureCount}/{_snapshot.History.CaptureCapacity} captures | {_snapshot.History.SessionCount} sessions | {_snapshot.History.EstimatedMemoryBytes / 1024.0 / 1024.0:0.0} MB";

    public string ExportText => _snapshot is null ? "-" : $"{_snapshot.Export.LastExportResult} | Failures {_snapshot.Export.LastExportFailureCount}";

    public string RecentErrorsText => _snapshot is null ? "-" : $"{_snapshot.Health.RecentErrorCount} recent warnings/errors";

    public string LogDirectoryText => _logDirectory;

    public void Refresh()
    {
        _snapshot = _diagnosticsService.CreateSnapshot(_stateFactory());
        OnPropertyChanged(nameof(HealthSummaryText));
        OnPropertyChanged(nameof(ApplicationText));
        OnPropertyChanged(nameof(RuntimeText));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(CameraText));
        OnPropertyChanged(nameof(AcquisitionText));
        OnPropertyChanged(nameof(ImagingText));
        OnPropertyChanged(nameof(HistoryText));
        OnPropertyChanged(nameof(ExportText));
        OnPropertyChanged(nameof(RecentErrorsText));
        RefreshLogEntries();
    }

    public void Dispose()
    {
        _timer.Stop();
        _logStore.EntryAdded -= OnLogEntryAdded;
    }

    private void CopyDiagnostics()
    {
        var snapshot = _snapshot ?? _diagnosticsService.CreateSnapshot(_stateFactory());
        Clipboard.SetText(_diagnosticsService.CreateTextReport(snapshot));
        SupportBundleStatus = "Diagnostics summary copied to clipboard.";
    }

    private async Task CreateSupportBundleAsync()
    {
        var snapshot = _snapshot ?? _diagnosticsService.CreateSnapshot(_stateFactory());
        IsCreatingSupportBundle = true;
        var progress = new Progress<SupportBundleProgress>(value => SupportBundleStatus = $"{value.Stage} ({value.Percentage:0}%).");
        try
        {
            var result = await _supportBundleService.CreateAsync(snapshot, new SupportBundleOptions(), progress);
            SupportBundleStatus = result.WasCanceled ? "Support bundle canceled." : $"Support bundle created: {result.BundlePath}";
        }
        catch (Exception exception)
        {
            SupportBundleStatus = $"Support bundle failed: {exception.Message}";
        }
        finally
        {
            IsCreatingSupportBundle = false;
        }
    }

    private void OpenLogFolder()
    {
        Directory.CreateDirectory(_logDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _logDirectory,
            UseShellExecute = true
        });
    }

    private void ClearLogView()
    {
        _logStore.Clear();
        RefreshLogEntries();
    }

    private void RefreshLogEntries()
    {
        if (IsPaused)
        {
            return;
        }

        var entries = _logStore.Query(new DiagnosticLogFilter(MinimumLogLevel, CategoryFilter, SearchText));
        LogEntries.Clear();
        foreach (var entry in entries.TakeLast(500))
        {
            LogEntries.Add(entry);
        }
    }

    private void OnLogEntryAdded(object? sender, DiagnosticLogEntry entry)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(RefreshLogEntries);
    }
}
