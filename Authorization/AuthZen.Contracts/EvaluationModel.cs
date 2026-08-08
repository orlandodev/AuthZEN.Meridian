using System.Text.Json.Serialization;

namespace AuthZen.Contracts;

// Subject-Action-Resource-Context (SARC) information model from the
// OpenID AuthZEN Authorization API 1.0 Final Specification.
// Extra attributes ride in the open-ended Properties bags.

public sealed record Subject
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Properties { get; init; }
}

public sealed record Resource
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Properties { get; init; }
}

public sealed record AuthZenAction
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Properties { get; init; }
}

// POST /access/v1/evaluation
public sealed record AccessEvaluationRequest
{
    [JsonPropertyName("subject")] public required Subject Subject { get; init; }
    [JsonPropertyName("action")] public required AuthZenAction Action { get; init; }
    [JsonPropertyName("resource")] public required Resource Resource { get; init; }
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Context { get; init; }
}

public sealed record AccessEvaluationResponse
{
    [JsonPropertyName("decision")] public required bool Decision { get; init; }
    // Optional reasons / obligations returned by the PDP.
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Context { get; init; }
}

// POST /access/v1/evaluations  (boxcarred / batch)
public sealed record AccessEvaluationsRequest
{
    [JsonPropertyName("subject")] public Subject? Subject { get; init; }
    [JsonPropertyName("action")] public AuthZenAction? Action { get; init; }
    [JsonPropertyName("resource")] public Resource? Resource { get; init; }
    [JsonPropertyName("context")] public Dictionary<string, object>? Context { get; init; }
    [JsonPropertyName("evaluations")] public required IReadOnlyList<EvaluationEntry> Evaluations { get; init; }
}

public sealed record AccessEvaluationsResponse
{
    [JsonPropertyName("evaluations")] public required IReadOnlyList<AccessEvaluationResponse> Evaluations { get; init; }
}
