using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DcamVision.Imaging.Diagnostics;

public sealed class DiagnosticsLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, DiagnosticsLogger> _loggers = new(StringComparer.Ordinal);
    private readonly DiagnosticsLoggingOptions _options;
    private readonly object _fileLock = new();
    private bool _disposed;

    public DiagnosticsLoggerProvider(InMemoryDiagnosticLogStore store, DiagnosticsLoggingOptions? options = null)
    {
        Store = store;
        _options = options ?? new DiagnosticsLoggingOptions();
        _options.Validate();
        LogDirectory = _options.LogDirectory ?? DiagnosticsPaths.DefaultLogDirectory();
        Directory.CreateDirectory(LogDirectory);
        PruneOldLogs();
    }

    public InMemoryDiagnosticLogStore Store { get; }

    public string LogDirectory { get; }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, category => new DiagnosticsLogger(category, this));
    }

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }

    private bool IsEnabled(LogLevel logLevel)
    {
        return !_disposed && logLevel != LogLevel.None && logLevel >= _options.MinimumLevel;
    }

    private void Write(DiagnosticLogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        Store.Add(entry);
        var line = FormatLogLine(entry);
        var path = Path.Combine(LogDirectory, $"dcam-vision-studio-{entry.Timestamp:yyyy-MM-dd}.log");
        lock (_fileLock)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private void PruneOldLogs()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetainedFileDays);
        foreach (var file in Directory.EnumerateFiles(LogDirectory, "dcam-vision-studio-*.log"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff.UtcDateTime)
            {
                info.Delete();
            }
        }
    }

    private static string FormatLogLine(DiagnosticLogEntry entry)
    {
        var properties = entry.Properties.Count == 0
            ? string.Empty
            : " " + string.Join(" ", entry.Properties.Select(property => $"{property.Key}={property.Value}"));
        var exception = entry.ExceptionType is null ? string.Empty : $" Exception={entry.ExceptionType}: {entry.ExceptionMessage}";
        return $"{entry.Timestamp:O} [{entry.Level}] {entry.Category}: {entry.Message}{properties}{exception}";
    }

    private sealed class DiagnosticsLogger : ILogger
    {
        private readonly string _category;
        private readonly DiagnosticsLoggerProvider _provider;

        public DiagnosticsLogger(string category, DiagnosticsLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _provider.IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var entry = new DiagnosticLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = logLevel,
                Category = _category,
                Message = Redactor.Redact(formatter(state, exception)),
                EventId = eventId.Id,
                ExceptionType = exception?.GetType().Name,
                ExceptionMessage = exception is null ? null : Redactor.Redact(exception.Message),
                StackTrace = exception?.StackTrace,
                Properties = ExtractProperties(state)
            };

            _provider.Write(entry);
        }

        private static IReadOnlyDictionary<string, string> ExtractProperties<TState>(TState state)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return new Dictionary<string, string>();
            }

            return pairs
                .Where(pair => pair.Key != "{OriginalFormat}")
                .ToDictionary(
                    pair => pair.Key,
                    pair => Redactor.Redact(Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
