using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Meridian.UnitTests.TestSupport;

public sealed record CapturedLogRecord(
    LogLevel Level,
    IReadOnlyDictionary<string, object?> Fields,
    Exception? Exception,
    ActivityTraceId? TraceId,
    ActivitySpanId? SpanId);
