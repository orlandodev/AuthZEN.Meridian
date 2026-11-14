using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Meridian.UnitTests.TestSupport;

// Records each log call's level, structured state, and the trace/span that
// was current when it was written — the same Activity.Current OpenTelemetry
// logging reads to stamp TraceId/SpanId on an exported log record.
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<CapturedLogRecord> _records = [];

    public IReadOnlyList<CapturedLogRecord> Records
    {
        get
        {
            lock (_records)
            {
                return [.. _records];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
            : [];

        lock (_records)
        {
            _records.Add(new CapturedLogRecord(
                logLevel, fields, exception, Activity.Current?.TraceId, Activity.Current?.SpanId));
        }
    }
}
