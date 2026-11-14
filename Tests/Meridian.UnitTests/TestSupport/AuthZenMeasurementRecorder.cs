using System.Diagnostics.Metrics;

namespace Meridian.UnitTests.TestSupport;

// Captures measurements from one named "Meridian.AuthZen" instrument. Other
// tests emit on the same process-wide meter in parallel, so only measurements
// matching a test-unique filter are kept — by default, filterTag equals
// filterValue. Measurement callbacks run synchronously on the recording
// thread, so a filter may also inspect Activity.Current (the span the
// measurement was recorded under) for values that never become metric tags.
public sealed class AuthZenMeasurementRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<AuthZenMeasurement> _measurements = [];
    private readonly Func<IReadOnlyDictionary<string, object?>, bool> _filter;

    public AuthZenMeasurementRecorder(string instrumentName, string filterTag, string filterValue)
        : this(instrumentName, tags => Equals(tags.GetValueOrDefault(filterTag), filterValue))
    {
    }

    public AuthZenMeasurementRecorder(string instrumentName, Func<IReadOnlyDictionary<string, object?>, bool> filter)
    {
        _filter = filter;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Meridian.AuthZen" && instrument.Name == instrumentName)
            {
                Instrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) => Record(value, tags));
        _listener.SetMeasurementEventCallback<double>((_, value, tags, _) => Record(value, tags));
        _listener.Start();
    }

    public Instrument? Instrument { get; private set; }

    public IReadOnlyList<AuthZenMeasurement> Measurements
    {
        get
        {
            lock (_measurements)
            {
                return [.. _measurements];
            }
        }
    }

    public void Dispose() => _listener.Dispose();

    private void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagMap = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            tagMap[tag.Key] = tag.Value;
        }

        if (!_filter(tagMap))
        {
            return;
        }

        lock (_measurements)
        {
            _measurements.Add(new AuthZenMeasurement(value, tagMap));
        }
    }
}
