using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Expense;
using Meridian.Pdp.Service.Receipts;
using Meridian.Pdp.Service.Reporting;

namespace Meridian.Pdp.Service.Pdp;

public sealed class PolicyRulesEngine(
    PolicyDbContext db,
    TimeProvider? timeProvider = null,
    TimeZoneInfo? businessTimeZone = null) : IPolicyEngine
{
    // Same ActivitySource/Meter name AuthZenPolicyDecisionClient uses
    // client-side — ServiceDefaults already subscribes every service to it,
    // so a trace connects the PEP's outbound span with this decision's span.
    private static readonly ActivitySource ActivitySource = new("Meridian.AuthZen");
    private static readonly Meter Meter = new("Meridian.AuthZen");
    private static readonly Counter<long> Decisions =
        Meter.CreateCounter<long>("authz.decisions", description: "Authorization decisions by outcome.");

    // One workspace per PolicyRulesEngine instance (itself Scoped), so its
    // profile cache lives for the whole HTTP call, including every entry of
    // a boxcarred request. The null defaults only apply to tests constructing
    // this directly without a service provider; a null businessTimeZone is fine
    // unless the test exercises an export rule (DepartmentSpendRules.CanExport
    // throws in that case).
    private readonly RuleWorkspace _workspace = new(
        db,
        timeProvider ?? TimeProvider.System,
        businessTimeZone);

    private static readonly Dictionary<
        (string ResourceType, string Action),
        Func<AccessEvaluationRequest, RuleWorkspace, CancellationToken, Task<bool>>> Rules = new()
    {
        [("expense", "create")] = ExpenseRules.CanCreate,
        [("expense", "read")] = ExpenseRules.CanRead,
        [("expense", "submit")] = ExpenseRules.CanSubmit,
        [("expense", "approve")] = ExpenseRules.CanDecide,
        [("expense", "reject")] = ExpenseRules.CanDecide,
        [("receipt", "read")] = ReceiptRules.CanRead,
        [("receipt", "create")] = ReceiptRules.CanCreate,
        [("department_spend", "read")] = DepartmentSpendRules.CanRead,
        [("department_spend", "export")] = DepartmentSpendRules.CanExport,
    };

    public async Task<bool> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("authz.decide", ActivityKind.Server);
        activity?.SetTag("authz.subject.id", request.Subject.Id);
        activity?.SetTag("authz.action", request.Action.Name);
        activity?.SetTag("authz.resource.type", request.Resource.Type);
        activity?.SetTag("authz.resource.id", request.Resource.Id);

        // Default-deny: an unmatched (resourceType, action) pair — including
        // malformed input — is denied with no DB call.
        var decision = Rules.TryGetValue((request.Resource.Type, request.Action.Name), out var rule)
            && await rule(request, _workspace, ct);

        activity?.SetTag("authz.decision", decision ? "permit" : "deny");
        // Enabled is false (skipping the tag-array allocation below) when
        // nothing is listening to the "Meridian.AuthZen" meter — mirrors the
        // null-conditional guard the Activity tags above get for free.
        if (Decisions.Enabled)
        {
            Decisions.Add(1,
                new KeyValuePair<string, object?>("decision", decision ? "permit" : "deny"),
                new KeyValuePair<string, object?>("action", request.Action.Name),
                new KeyValuePair<string, object?>("resource.type", request.Resource.Type));
        }

        return decision;
    }
}
