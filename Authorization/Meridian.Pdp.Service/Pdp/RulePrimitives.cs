using System.Text.Json;
using AuthZen.Contracts;

namespace Meridian.Pdp.Service.Pdp;

// Pure, DB-free helpers for reading SARC properties. Values deserialize as
// boxed JsonElement, not string/decimal directly — centralized here so that
// conversion only happens in one place.
public static class RulePrimitives
{
    public static bool IsOwner(AccessEvaluationRequest request)
    {
        var ownerId = GetOwnerId(request);
        return ownerId is not null && string.Equals(ownerId, request.Subject.Id, StringComparison.Ordinal);
    }

    public static string? GetOwnerId(AccessEvaluationRequest request) =>
        GetStringProperty(request.Resource.Properties, "ownerId");

    public static string? GetResourceStatus(AccessEvaluationRequest request) =>
        GetStringProperty(request.Resource.Properties, "status");

    public static string? GetResourceDepartment(AccessEvaluationRequest request) =>
        GetStringProperty(request.Resource.Properties, "department");

    public static bool TryGetContextAmount(AccessEvaluationRequest request, out decimal amount)
    {
        amount = 0m;
        if (request.Context is null || !request.Context.TryGetValue("amount", out var raw) || raw is null)
        {
            return false;
        }

        return TryToDecimal(raw, out amount);
    }

    private static string? GetStringProperty(Dictionary<string, object>? props, string key)
    {
        if (props is null || !props.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
            _ => raw.ToString()
        };
    }

    private static bool TryToDecimal(object raw, out decimal value)
    {
        switch (raw)
        {
            case decimal d:
                value = d;
                return true;
            case double d:
                value = (decimal)d;
                return true;
            case int i:
                value = i;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } je when je.TryGetDecimal(out var d):
                value = d;
                return true;
            default:
                value = 0m;
                return false;
        }
    }
}
