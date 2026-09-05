using System.Collections.ObjectModel;
using System.Globalization;
using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.App.ViewModels;

public sealed class CaptureHistoryViewModel : ObservableObject
{
    private readonly CaptureHistoryStore _store;
    private readonly Func<bool> _confirmClearHistory;
    private string _searchText = string.Empty;
    private CaptureSessionViewModel _selectedSession = CaptureSessionViewModel.All;
    private CaptureSource? _selectedSource;
    private bool _taggedOnly;
    private bool _saturatedOnly;
    private CaptureHistorySortMode _selectedSortMode = CaptureHistorySortMode.NewestFirst;
    private CaptureRecordViewModel? _selectedCapture;
    private CaptureRecordViewModel? _compareA;
    private CaptureRecordViewModel? _compareB;
    private string _notesText = string.Empty;
    private string _tagText = string.Empty;
    private string _sessionName = string.Empty;

    public CaptureHistoryViewModel(CaptureHistoryStore store, Func<bool>? confirmClearHistory = null)
    {
        _store = store;
        _confirmClearHistory = confirmClearHistory ?? (() => true);
        NewSessionCommand = new RelayCommand(NewSession);
        RenameSessionCommand = new RelayCommand(RenameSession, () => _store.ActiveSession is not null && !string.IsNullOrWhiteSpace(SessionName));
        SaveNotesCommand = new RelayCommand(SaveNotes, () => SelectedCapture is not null);
        AddTagCommand = new RelayCommand(AddTag, () => SelectedCapture is not null && !string.IsNullOrWhiteSpace(TagText));
        RemoveTagCommand = new ParameterRelayCommand(RemoveTag, tag => SelectedCapture is not null && tag is string);
        DeleteCaptureCommand = new RelayCommand(DeleteSelectedCapture, () => SelectedCapture is not null);
        ClearHistoryCommand = new RelayCommand(ClearHistory, () => Captures.Count > 0);
        SetCompareACommand = new RelayCommand(SetCompareA, () => SelectedCapture is not null);
        SetCompareBCommand = new RelayCommand(SetCompareB, () => SelectedCapture is not null);

        RefreshFromStore();
    }

    public ObservableCollection<CaptureRecordViewModel> Captures { get; } = [];

    public ObservableCollection<CaptureSessionViewModel> Sessions { get; } = [CaptureSessionViewModel.All];

    public IReadOnlyList<CaptureSource?> SourceOptions { get; } = [null, CaptureSource.Manual, CaptureSource.Live, CaptureSource.ExposureSweep];

    public IReadOnlyList<CaptureHistorySortMode> SortModes { get; } =
    [
        CaptureHistorySortMode.NewestFirst,
        CaptureHistorySortMode.OldestFirst,
        CaptureHistorySortMode.ExposureLowToHigh,
        CaptureHistorySortMode.ExposureHighToLow,
        CaptureHistorySortMode.MeanIntensity,
        CaptureHistorySortMode.SaturationPercentage
    ];

    public RelayCommand NewSessionCommand { get; }

    public RelayCommand RenameSessionCommand { get; }

    public RelayCommand SaveNotesCommand { get; }

    public RelayCommand AddTagCommand { get; }

    public ParameterRelayCommand RemoveTagCommand { get; }

    public RelayCommand DeleteCaptureCommand { get; }

    public RelayCommand ClearHistoryCommand { get; }

    public RelayCommand SetCompareACommand { get; }

    public RelayCommand SetCompareBCommand { get; }

    public event EventHandler<CaptureRecord>? SelectedCaptureChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshCaptures();
            }
        }
    }

    public CaptureSessionViewModel SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                RefreshCaptures();
            }
        }
    }

    public CaptureSource? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                RefreshCaptures();
            }
        }
    }

    public bool TaggedOnly
    {
        get => _taggedOnly;
        set
        {
            if (SetProperty(ref _taggedOnly, value))
            {
                RefreshCaptures();
            }
        }
    }

    public bool SaturatedOnly
    {
        get => _saturatedOnly;
        set
        {
            if (SetProperty(ref _saturatedOnly, value))
            {
                RefreshCaptures();
            }
        }
    }

    public CaptureHistorySortMode SelectedSortMode
    {
        get => _selectedSortMode;
        set
        {
            if (SetProperty(ref _selectedSortMode, value))
            {
                RefreshCaptures();
            }
        }
    }

    public CaptureRecordViewModel? SelectedCapture
    {
        get => _selectedCapture;
        set
        {
            if (SetProperty(ref _selectedCapture, value))
            {
                NotesText = value?.Record.Notes ?? string.Empty;
                RefreshSelectionCommands();
                if (value is not null)
                {
                    SelectedCaptureChanged?.Invoke(this, value.Record);
                }
            }
        }
    }

    public string NotesText
    {
        get => _notesText;
        set => SetProperty(ref _notesText, value);
    }

    public string TagText
    {
        get => _tagText;
        set
        {
            if (SetProperty(ref _tagText, value))
            {
                AddTagCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SessionName
    {
        get => _sessionName;
        set
        {
            if (SetProperty(ref _sessionName, value))
            {
                RenameSessionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CaptureRecordViewModel? CompareA
    {
        get => _compareA;
        private set
        {
            if (SetProperty(ref _compareA, value))
            {
                RefreshCompareFlags();
                OnPropertyChanged(nameof(ComparisonText));
            }
        }
    }

    public CaptureRecordViewModel? CompareB
    {
        get => _compareB;
        private set
        {
            if (SetProperty(ref _compareB, value))
            {
                RefreshCompareFlags();
                OnPropertyChanged(nameof(ComparisonText));
            }
        }
    }

    public string ActiveSessionText => _store.ActiveSession?.Name ?? "No active session";

    public string SummaryText => $"Captures: {_store.Captures.Count} / {_store.MaximumCaptures} | Sessions: {_store.Sessions.Count} | Memory: ~{_store.EstimatedMemoryBytes / 1024.0 / 1024.0:0.0} MB";

    public string FilterSummaryText => Captures.Count == 0 ? "No captures match these filters." : $"Showing {Captures.Count} capture(s).";

    public string ComparisonText
    {
        get
        {
            if (CompareA is null || CompareB is null)
            {
                return "Select Compare A and Compare B.";
            }

            var comparison = CaptureComparisonCalculator.Compare(CompareA.Record, CompareB.Record);
            var mad = comparison.MeanAbsolutePixelDifference is null
                ? "n/a"
                : comparison.MeanAbsolutePixelDifference.Value.ToString("0.0", CultureInfo.InvariantCulture);
            return $"Exposure Δ {ExposureUnitConverter.Format(comparison.ExposureDifference)} | Mean Δ {comparison.MeanDifference:0.0} | Saturation Δ {comparison.SaturationDifference:0.0}% | Mean Abs Pixel Δ {mad}";
        }
    }

    public string SelectedCaptureIdText => SelectedCapture?.CaptureId.ToString() ?? "-";

    public string SelectedCaptureSessionText => SelectedCapture?.SessionName ?? "-";

    public string SelectedCaptureSourceText => SelectedCapture?.SourceText ?? "-";

    public string SelectedCaptureTimestampText => SelectedCapture?.TimestampText ?? "-";

    public string SelectedCaptureCameraText => SelectedCapture is null
        ? "-"
        : $"{SelectedCapture.Record.Metadata.Manufacturer} {SelectedCapture.Record.Metadata.Model} ({SelectedCapture.Record.Metadata.SerialNumber})";

    public string SelectedCaptureExposureText => SelectedCapture?.ExposureText ?? "-";

    public string SelectedCaptureGainText => SelectedCapture is null
        ? "-"
        : $"{SelectedCapture.Record.Metadata.Gain:0.###}x";

    public string SelectedCaptureResolutionText => SelectedCapture is null
        ? "-"
        : $"{SelectedCapture.Record.Metadata.Width} x {SelectedCapture.Record.Metadata.Height}";

    public string SelectedCaptureFormatText => SelectedCapture?.Record.Metadata.PixelFormat.ToString() ?? "-";

    public string SelectedCaptureTriggerText => SelectedCapture?.Record.Metadata.TriggerMode ?? "-";

    public string SelectedCaptureStatisticsText => SelectedCapture is null
        ? "-"
        : $"Min {SelectedCapture.Record.Statistics.Minimum} | Max {SelectedCapture.Record.Statistics.Maximum} | Mean {SelectedCapture.Record.Statistics.Mean:0.0} | Saturation {SelectedCapture.Record.Statistics.SaturationPercentage:0.0}%";

    public string SelectedCapturePropertySnapshotText => SelectedCapture is null
        ? "-"
        : string.Join(" | ", SelectedCapture.Record.Metadata.PropertySnapshot.Take(8).Select(pair => $"{pair.Key}: {pair.Value}"));

    public void RefreshFromStore()
    {
        RefreshSessions();
        RefreshCaptures();
        OnPropertyChanged(nameof(ActiveSessionText));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void RefreshSessions()
    {
        var selectedId = SelectedSession.SessionId;
        Sessions.Clear();
        Sessions.Add(CaptureSessionViewModel.All);
        foreach (var session in _store.Sessions.OrderByDescending(session => session.StartedAt))
        {
            Sessions.Add(CaptureSessionViewModel.FromSession(session));
        }

        SelectedSession = Sessions.FirstOrDefault(session => session.SessionId == selectedId) ?? CaptureSessionViewModel.All;
        SessionName = _store.ActiveSession?.Name ?? string.Empty;
    }

    private void RefreshCaptures()
    {
        var selectedId = SelectedCapture?.CaptureId;
        var sessions = _store.Sessions.ToDictionary(session => session.SessionId);
        var captures = _store.Query(new CaptureHistoryFilter
        {
            SearchText = SearchText,
            SessionId = SelectedSession.SessionId,
            Source = SelectedSource,
            TaggedOnly = TaggedOnly,
            SaturatedOnly = SaturatedOnly,
            SortMode = SelectedSortMode
        });

        Captures.Clear();
        foreach (var capture in captures)
        {
            sessions.TryGetValue(capture.SessionId, out var session);
            Captures.Add(new CaptureRecordViewModel(capture, session));
        }

        SelectedCapture = Captures.FirstOrDefault(capture => capture.CaptureId == selectedId);
        OnPropertyChanged(nameof(FilterSummaryText));
        OnPropertyChanged(nameof(SummaryText));
        ClearMissingCompareSelections();
        ClearHistoryCommand.RaiseCanExecuteChanged();
    }

    private void NewSession()
    {
        _store.StartNewSession();
        RefreshFromStore();
    }

    private void RenameSession()
    {
        var active = _store.ActiveSession;
        if (active is null)
        {
            return;
        }

        _store.RenameSession(active.SessionId, SessionName);
        RefreshFromStore();
    }

    private void SaveNotes()
    {
        if (SelectedCapture is null)
        {
            return;
        }

        _store.UpdateNotes(SelectedCapture.CaptureId, NotesText);
        RefreshFromStore();
    }

    private void AddTag()
    {
        if (SelectedCapture is null)
        {
            return;
        }

        _store.AddTag(SelectedCapture.CaptureId, TagText);
        TagText = string.Empty;
        RefreshFromStore();
    }

    private void RemoveTag(object? parameter)
    {
        if (SelectedCapture is null || parameter is not string tag)
        {
            return;
        }

        _store.RemoveTag(SelectedCapture.CaptureId, tag);
        RefreshFromStore();
    }

    private void DeleteSelectedCapture()
    {
        if (SelectedCapture is null)
        {
            return;
        }

        _store.DeleteCapture(SelectedCapture.CaptureId);
        RefreshFromStore();
    }

    private void ClearHistory()
    {
        if (!_confirmClearHistory())
        {
            return;
        }

        _store.Clear();
        CompareA = null;
        CompareB = null;
        RefreshFromStore();
    }

    private void SetCompareA()
    {
        CompareA = SelectedCapture;
    }

    private void SetCompareB()
    {
        CompareB = SelectedCapture;
    }

    private void RefreshSelectionCommands()
    {
        SaveNotesCommand.RaiseCanExecuteChanged();
        AddTagCommand.RaiseCanExecuteChanged();
        DeleteCaptureCommand.RaiseCanExecuteChanged();
        SetCompareACommand.RaiseCanExecuteChanged();
        SetCompareBCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedCaptureIdText));
        OnPropertyChanged(nameof(SelectedCaptureSessionText));
        OnPropertyChanged(nameof(SelectedCaptureSourceText));
        OnPropertyChanged(nameof(SelectedCaptureTimestampText));
        OnPropertyChanged(nameof(SelectedCaptureCameraText));
        OnPropertyChanged(nameof(SelectedCaptureExposureText));
        OnPropertyChanged(nameof(SelectedCaptureGainText));
        OnPropertyChanged(nameof(SelectedCaptureResolutionText));
        OnPropertyChanged(nameof(SelectedCaptureFormatText));
        OnPropertyChanged(nameof(SelectedCaptureTriggerText));
        OnPropertyChanged(nameof(SelectedCaptureStatisticsText));
        OnPropertyChanged(nameof(SelectedCapturePropertySnapshotText));
    }

    private void RefreshCompareFlags()
    {
        foreach (var capture in Captures)
        {
            capture.IsCompareA = CompareA?.CaptureId == capture.CaptureId;
            capture.IsCompareB = CompareB?.CaptureId == capture.CaptureId;
        }
    }

    private void ClearMissingCompareSelections()
    {
        if (CompareA is not null && _store.GetCapture(CompareA.CaptureId) is null)
        {
            CompareA = null;
        }

        if (CompareB is not null && _store.GetCapture(CompareB.CaptureId) is null)
        {
            CompareB = null;
        }

        RefreshCompareFlags();
    }
}
