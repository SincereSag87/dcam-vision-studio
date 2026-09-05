namespace DcamVision.Imaging;

public sealed class CaptureHistoryStore
{
    private readonly object _syncRoot = new();
    private readonly CaptureHistoryOptions _options;
    private readonly List<CaptureRecord> _captures = [];
    private readonly List<CaptureSession> _sessions = [];
    private long _nextSequenceNumber;

    public CaptureHistoryStore()
        : this(new CaptureHistoryOptions())
    {
    }

    public CaptureHistoryStore(CaptureHistoryOptions options)
    {
        options.Validate();
        _options = options;
    }

    public event EventHandler<CaptureRecord>? CaptureAdded;

    public event EventHandler<CaptureRecord>? CaptureRemoved;

    public event EventHandler? HistoryCleared;

    public event EventHandler<CaptureSession>? SessionChanged;

    public int MaximumCaptures => _options.MaximumCaptures;

    public CaptureSession? ActiveSession { get; private set; }

    public IReadOnlyList<CaptureRecord> Captures
    {
        get
        {
            lock (_syncRoot)
            {
                return _captures.ToArray();
            }
        }
    }

    public IReadOnlyList<CaptureSession> Sessions
    {
        get
        {
            lock (_syncRoot)
            {
                return _sessions.ToArray();
            }
        }
    }

    public long EstimatedMemoryBytes
    {
        get
        {
            lock (_syncRoot)
            {
                return _captures.Sum(capture => (long)capture.Frame.Width * capture.Frame.Height * sizeof(ushort));
            }
        }
    }

    public CaptureSession EnsureActiveSession()
    {
        lock (_syncRoot)
        {
            if (ActiveSession is not null)
            {
                return ActiveSession;
            }
        }

        return StartNewSession();
    }

    public CaptureSession StartNewSession(string? name = null)
    {
        var now = DateTimeOffset.UtcNow;
        CaptureSession session;
        lock (_syncRoot)
        {
            session = new CaptureSession
            {
                SessionId = Guid.NewGuid(),
                StartedAt = now,
                Name = NormalizeSessionName(name, now)
            };
            _sessions.Add(session);
            ActiveSession = session;
        }

        SessionChanged?.Invoke(this, session);
        return session;
    }

    public CaptureSession RenameSession(Guid sessionId, string name)
    {
        var trimmed = ValidateSessionName(name);
        CaptureSession updated;
        lock (_syncRoot)
        {
            var index = _sessions.FindIndex(session => session.SessionId == sessionId);
            if (index < 0)
            {
                throw new InvalidOperationException("Capture session was not found.");
            }

            updated = _sessions[index] with { Name = trimmed };
            _sessions[index] = updated;
            if (ActiveSession?.SessionId == sessionId)
            {
                ActiveSession = updated;
            }
        }

        SessionChanged?.Invoke(this, updated);
        return updated;
    }

    public CaptureRecord AddCapture(CaptureRecord capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        capture.Frame.Validate();

        var snapshot = capture with
        {
            Frame = CaptureRecordFactory.CloneFrame(capture.Frame),
            SequenceNumber = capture.SequenceNumber <= 0 ? Interlocked.Increment(ref _nextSequenceNumber) : capture.SequenceNumber,
            Tags = NormalizeTags(capture.Tags)
        };

        CaptureRecord? removed = null;
        lock (_syncRoot)
        {
            if (!_sessions.Any(session => session.SessionId == snapshot.SessionId))
            {
                _sessions.Add(new CaptureSession
                {
                    SessionId = snapshot.SessionId,
                    StartedAt = snapshot.CapturedAt,
                    Name = $"Session {snapshot.CapturedAt:yyyy-MM-dd HH:mm}"
                });
            }

            _captures.Add(snapshot);
            if (_captures.Count > _options.MaximumCaptures)
            {
                removed = _captures.OrderBy(captureRecord => captureRecord.SequenceNumber).First();
                _captures.Remove(removed);
            }
        }

        CaptureAdded?.Invoke(this, snapshot);
        if (removed is not null)
        {
            CaptureRemoved?.Invoke(this, removed);
        }

        return snapshot;
    }

    public CaptureRecord? GetCapture(Guid captureId)
    {
        lock (_syncRoot)
        {
            return _captures.FirstOrDefault(capture => capture.CaptureId == captureId);
        }
    }

    public bool DeleteCapture(Guid captureId)
    {
        CaptureRecord? removed;
        lock (_syncRoot)
        {
            removed = _captures.FirstOrDefault(capture => capture.CaptureId == captureId);
            if (removed is null)
            {
                return false;
            }

            _captures.Remove(removed);
        }

        CaptureRemoved?.Invoke(this, removed);
        return true;
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _captures.Clear();
            _sessions.Clear();
            ActiveSession = null;
        }

        HistoryCleared?.Invoke(this, EventArgs.Empty);
    }

    public CaptureRecord UpdateNotes(Guid captureId, string notes)
    {
        return UpdateCapture(captureId, capture => capture with { Notes = notes.Trim() });
    }

    public CaptureRecord AddTag(Guid captureId, string tag)
    {
        var normalized = NormalizeTag(tag);
        return UpdateCapture(captureId, capture =>
        {
            if (capture.Tags.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return capture;
            }

            return capture with { Tags = [.. capture.Tags, normalized] };
        });
    }

    public CaptureRecord RemoveTag(Guid captureId, string tag)
    {
        var normalized = NormalizeTag(tag);
        return UpdateCapture(captureId, capture => capture with
        {
            Tags = capture.Tags
                .Where(existing => !string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        });
    }

    public IReadOnlyList<CaptureRecord> Query(CaptureHistoryFilter filter)
    {
        return CaptureHistoryQuery.Apply(Captures, Sessions, filter);
    }

    private CaptureRecord UpdateCapture(Guid captureId, Func<CaptureRecord, CaptureRecord> update)
    {
        lock (_syncRoot)
        {
            var index = _captures.FindIndex(capture => capture.CaptureId == captureId);
            if (index < 0)
            {
                throw new InvalidOperationException("Capture was not found.");
            }

            var updated = update(_captures[index]);
            _captures[index] = updated;
            return updated;
        }
    }

    private static string NormalizeSessionName(string? name, DateTimeOffset timestamp)
    {
        return string.IsNullOrWhiteSpace(name)
            ? $"Session {timestamp:yyyy-MM-dd HH:mm}"
            : ValidateSessionName(name);
    }

    private static string ValidateSessionName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Session name is required.", nameof(name));
        }

        if (trimmed.Length > 80)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Session name must be 80 characters or fewer.");
        }

        return trimmed;
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
    {
        var normalized = new List<string>();
        foreach (var tag in tags)
        {
            var candidate = NormalizeTag(tag);
            if (!normalized.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                normalized.Add(candidate);
            }
        }

        return normalized;
    }

    private static string NormalizeTag(string tag)
    {
        var normalized = string.Join(' ', tag.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Tag cannot be empty.", nameof(tag));
        }

        return normalized;
    }
}
