namespace DcamVision.Imaging.Diagnostics;

public sealed class InMemoryDiagnosticLogStore
{
    private readonly object _syncRoot = new();
    private readonly Queue<DiagnosticLogEntry> _entries = new();
    private readonly Queue<DiagnosticLogEntry> _errors = new();

    public InMemoryDiagnosticLogStore(int capacity = 1000, int errorCapacity = 100)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Log capacity must be positive.");
        }

        if (errorCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCapacity), "Error capacity must be positive.");
        }

        Capacity = capacity;
        ErrorCapacity = errorCapacity;
    }

    public event EventHandler<DiagnosticLogEntry>? EntryAdded;

    public int Capacity { get; }

    public int ErrorCapacity { get; }

    public IReadOnlyList<DiagnosticLogEntry> Entries
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.ToArray();
            }
        }
    }

    public IReadOnlyList<DiagnosticLogEntry> RecentErrors
    {
        get
        {
            lock (_syncRoot)
            {
                return _errors.ToArray();
            }
        }
    }

    public void Add(DiagnosticLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_syncRoot)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }

            if (entry.IsError)
            {
                _errors.Enqueue(entry);
                while (_errors.Count > ErrorCapacity)
                {
                    _errors.Dequeue();
                }
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    public IReadOnlyList<DiagnosticLogEntry> Query(DiagnosticLogFilter filter)
    {
        var entries = Entries.Where(entry => entry.Level >= filter.MinimumLevel);

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            entries = entries.Where(entry => entry.Category.Contains(filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            entries = entries.Where(entry =>
                entry.Message.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) ||
                entry.Category.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) ||
                entry.Properties.Any(property =>
                    property.Key.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    property.Value.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase)));
        }

        return entries.ToArray();
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
            _errors.Clear();
        }
    }
}
