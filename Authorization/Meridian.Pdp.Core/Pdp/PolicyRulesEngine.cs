using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Core.Expense;
using Meridian.Pdp.Core.Receipts;
using Meridian.Pdp.Core.Reporting;

namespace Meridian.Pdp.Core.Pdp;

public sealed class PolicyRulesEngine(
    PolicyDbContext db,
    TimeProvider? timeProvider = null,
    TimeZoneInfo? businessTimeZone = null) : IPolicyEngine
{
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
        (string PolicyId, Func<AccessEvaluationRequest, RuleWorkspace, CancellationToken, Task<bool>> Rule)> Rules = new()
    {
        [("expense", "create")] = (PolicyIds.ExpenseCreate, ExpenseRules.CanCreate),
        [("expense", "read")] = (PolicyIds.ExpenseRead, ExpenseRules.CanRead),
        [("expense", "submit")] = (PolicyIds.ExpenseSubmit, ExpenseRules.CanSubmit),
        [("expense", "approve")] = (PolicyIds.ExpenseDecide, ExpenseRules.CanDecide),
        [("expense", "reject")] = (PolicyIds.ExpenseDecide, ExpenseRules.CanDecide),
        [("receipt", "read")] = (PolicyIds.ReceiptRead, ReceiptRules.CanRead),
        [("receipt", "create")] = (PolicyIds.ReceiptCreate, ReceiptRules.CanCreate),
        [("department_spend", "read")] = (PolicyIds.DepartmentSpendRead, DepartmentSpendRules.CanRead),
        [("department_spend", "export")] = (PolicyIds.DepartmentSpendExport, DepartmentSpendRules.CanExport),
    };

    public async Task<PolicyDecision> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        // Default-deny: an unmatched (resourceType, action) pair — including
        // malformed input — is denied with no DB call and no policy ID.
        if (!Rules.TryGetValue((request.Resource.Type, request.Action.Name), out var entry))
        {
            return new PolicyDecision(false, null);
        }

        return new PolicyDecision(await entry.Rule(request, _workspace, ct), entry.PolicyId);
    }
}
