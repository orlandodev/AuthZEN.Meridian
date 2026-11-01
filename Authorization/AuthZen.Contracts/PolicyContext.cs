using System.Text.Json;

namespace AuthZen.Contracts;

// Reads and writes the engine-neutral policy ID a PDP reports in an
// AccessEvaluationResponse's context, as { "policy": { "id": "expense.read" } }.
// The ID names the policy that governed the decision (permit or deny) and is
// meant for telemetry only — PEPs never surface it to end users.
public static class PolicyContext
{
    private const string PolicyKey = "policy";
    private const string IdKey = "id";

    public static Dictionary<string, object>? Create(string? policyId)
    {
        if (policyId is null)
        {
            return null;
        }

        return new Dictionary<string, object>
        {
            [PolicyKey] = new Dictionary<string, object> { [IdKey] = policyId }
        };
    }

    // Handles both the in-memory shape Create builds and the JsonElement
    // values a deserialized response carries.
    public static string? GetPolicyId(this AccessEvaluationResponse response)
    {
        if (response.Context is null || !response.Context.TryGetValue(PolicyKey, out var policy))
        {
            return null;
        }

        return policy switch
        {
            JsonElement { ValueKind: JsonValueKind.Object } element
                when element.TryGetProperty(IdKey, out var id) && id.ValueKind == JsonValueKind.String
                => id.GetString(),
            IReadOnlyDictionary<string, object> dictionary
                when dictionary.TryGetValue(IdKey, out var id)
                => id as string,
            _ => null
        };
    }
}
