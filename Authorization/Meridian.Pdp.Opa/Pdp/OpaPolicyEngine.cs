using System.Net.Http.Json;
using System.Text.Json;
using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Core.Pdp;

namespace Meridian.Pdp.Opa.Pdp;

// The translator half of the OPA-backed PDP: gathers the same policydb facts
// RuleWorkspace already exposes to the homegrown engine, hands them to a real
// OPA server as an `input` document, and relays back whatever OPA decides.
// No rule logic lives here — see policies/*.rego for that. See README.md for
// the end-to-end request flow and where the OPA container itself comes from.
public sealed class OpaPolicyEngine(
    HttpClient opaClient,
    PolicyDbContext db,
    TimeZoneInfo businessTimeZone,
    TimeProvider? timeProvider = null) : IPolicyEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RuleWorkspace _workspace = new(db, timeProvider ?? TimeProvider.System, businessTimeZone);

    // OPA's time builtins only understand IANA zone names, while
    // TimeZoneInfo.FindSystemTimeZoneById on Windows also accepts Windows ids
    // ("Eastern Standard Time") — normalize once so either config form works.
    private readonly string _businessTimeZoneIanaId = ToIanaId(businessTimeZone);

    public async Task<bool> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        var input = await BuildInputAsync(request, ct);

        using var response = await opaClient.PostAsJsonAsync(
            "v1/data/meridian/authz/allow", new { input }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OpaDataResponse>(JsonOptions, ct);

        // OPA omits "result" entirely when the queried rule is undefined
        // (e.g. no policy matched) — fail-closed, mirroring PolicyRulesEngine's
        // default-deny on an unmatched (resourceType, action) pair.
        return body?.Result ?? false;
    }

    // Gathers every fact any policies/*.rego file might need, rather than
    // duplicating PolicyRulesEngine's per-(resourceType, action) dispatch
    // here just to fetch facts selectively. Costs a couple of extra
    // single-row queries per call versus the homegrown engine's precise
    // fetch — an acceptable trade for keeping this translator ignorant of
    // which specific rule Rego will run.
    private async Task<OpaInput> BuildInputAsync(AccessEvaluationRequest request, CancellationToken ct)
    {
        var subject = await _workspace.GetProfileAsync(request.Subject.Id, ct);

        var ownerId = RulePrimitives.GetOwnerId(request);
        var isManagerOf = ownerId is not null
            && subject is not null
            && await _workspace.IsManagerOfAsync(subject.UserId, ownerId, ct);

        var amountLimit = await _workspace.GetAmountLimitAsync(
            PolicyConstants.AmountLimitKeys.ExpenseApproveManagerLimit, ct);

        return new OpaInput(
            Subject: new OpaSubject(request.Subject.Id, subject?.Role, subject?.Department),
            Resource: new OpaResource(request.Resource.Type, request.Resource.Id, request.Resource.Properties),
            Action: request.Action.Name,
            Context: request.Context,
            Facts: new OpaFacts(
                IsManagerOf: isManagerOf,
                AmountLimit: amountLimit,
                Now: _workspace.TimeProvider.GetUtcNow(),
                BusinessTimeZone: _businessTimeZoneIanaId));
    }

    private static string ToIanaId(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
        {
            return zone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaId)
            ? ianaId
            : throw new InvalidOperationException($"Business timezone '{zone.Id}' has no IANA equivalent for OPA.");
    }

    private sealed record OpaInput(
        OpaSubject Subject, OpaResource Resource, string Action,
        Dictionary<string, object>? Context, OpaFacts Facts);

    private sealed record OpaSubject(string Id, string? Role, string? Department);

    private sealed record OpaResource(string Type, string? Id, Dictionary<string, object>? Properties);

    private sealed record OpaFacts(bool IsManagerOf, decimal AmountLimit, DateTimeOffset Now, string BusinessTimeZone);

    private sealed record OpaDataResponse(bool? Result);
}
