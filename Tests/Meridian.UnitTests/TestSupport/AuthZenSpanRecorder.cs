using System.Diagnostics;

namespace Meridian.UnitTests.TestSupport;

// Captures stopped "Meridian.AuthZen" spans with the given operation name
// that match a test-unique filter (by default, filterTag equals filterValue),
// since other tests emit on the same process-wide source in parallel.
public sealed class AuthZenSpanRecorder : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _spans = [];

    public AuthZenSpanRecorder(string operationName, string filterTag, object filterValue)
        : this(operationName, activity => Equals(activity.GetTagItem(filterTag), filterValue))
    {
    }

    public AuthZenSpanRecorder(string operationName, Func<Activity, bool> filter)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Meridian.AuthZen",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == operationName && filter(activity))
                {
                    lock (_spans)
                    {
                        _spans.Add(activity);
                    }
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public Activity Single()
    {
        lock (_spans)
        {
            _spans.Should().ContainSingle();
            return _spans[0];
        }
    }

    public void Dispose() => _listener.Dispose();
}
