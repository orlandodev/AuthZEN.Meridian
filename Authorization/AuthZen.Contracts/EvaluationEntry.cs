using System.Text.Json.Serialization;

namespace AuthZen.Contracts;

// One entry within a boxcarred POST /access/v1/evaluations request. Unlike
// AccessEvaluationRequest (used for the single-evaluation endpoint), every
// field here is optional: per the AuthZEN boxcar spec, an entry that omits
// subject/action/resource/context inherits the corresponding top-level
// default from the enclosing AccessEvaluationsRequest.
public sealed record EvaluationEntry
{
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Subject? Subject { get; init; }
    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuthZenAction? Action { get; init; }
    [JsonPropertyName("resource")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Resource? Resource { get; init; }
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Context { get; init; }
}
