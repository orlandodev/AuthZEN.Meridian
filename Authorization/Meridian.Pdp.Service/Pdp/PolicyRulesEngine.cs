using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Expense;
using Meridian.Pdp.Service.Receipts;
using Meridian.Pdp.Service.Reporting;

namespace Meridian.Pdp.Service.Pdp;

public sealed class PolicyRulesEngine(PolicyDbContext db, TimeProvider? timeProvider = null) : IPolicyEngine
{
    // One workspace per PolicyRulesEngine instance, not per EvaluateAsync
    // call: PolicyRulesEngine is registered Scoped, so this instance (and
    // its RuleWorkspace's subject-profile cache) lives for the whole HTTP
    // call — including every entry of a boxcarred /access/v1/evaluations
    // request. DI supplies the host's real TimeProvider (auto-registered by
    // the generic host since .NET 8); the null-default only kicks in for
    // tests constructing this directly without a service provider.
    private readonly RuleWorkspace _workspace = new(db, timeProvider ?? TimeProvider.System);

    private static readonly Dictionary<
        (string ResourceType, string Action),
        Func<AccessEvaluationRequest, RuleWorkspace, CancellationToken, Task<bool>>> Rules = new()
    {
        [("expense", "read")] = ExpenseRules.CanRead,
        [("expense", "approve")] = ExpenseRules.CanDecide,
        [("expense", "reject")] = ExpenseRules.CanDecide,
        [("receipt", "read")] = ReceiptRules.CanRead,
        [("department_spend", "read")] = DepartmentSpendRules.CanRead,
        [("department_spend", "export")] = DepartmentSpendRules.CanExport,
    };

    public Task<bool> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        // Default-deny: any (resourceType, action) pair not explicitly
        // listed above is denied, with no exceptions and no DB call — this
        // also covers malformed or unrecognized action/resource-type
        // strings from a caller.
        if (!Rules.TryGetValue((request.Resource.Type, request.Action.Name), out var rule))
        {
            return Task.FromResult(false);
        }

        return rule(request, _workspace, ct);
    }
}
