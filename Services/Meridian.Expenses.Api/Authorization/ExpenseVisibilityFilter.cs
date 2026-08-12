using System.Security.Claims;
using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Services;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;

namespace Meridian.Expenses.Api.Authorization;

// Narrows IExpenseService.GetVisibleExpensesAsync's manager-branch candidates
// (today, everyone in the manager's own department) down to the same set
// OwnerOrPrivilegedHandler would allow one-by-one via the PDP's "read" rule —
// an explicit ManagerOf relationship, not a department match — so the list
// and detail endpoints never disagree about what a manager can see. Finance
// (unconditional in both ExpenseService and ExpenseRules.CanRead) and
// non-manager (owner-only, trivially symmetric with IsOwner) candidate sets
// pass through unfiltered; only the manager branch can diverge from
// ExpenseRules.CanRead, so only it needs the extra PDP round trip.
public sealed class ExpenseVisibilityFilter(IExpenseService expenses, IPolicyDecisionClient pdp)
{
    public async Task<IReadOnlyList<ExpenseDto>> GetVisibleExpensesAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var caller = user.ToCallerContext();
        var candidates = await expenses.GetVisibleExpensesAsync(caller, ct);

        if (!caller.IsManager || caller.IsFinance || candidates.Count == 0)
        {
            return candidates;
        }

        var batch = new AccessEvaluationsRequest
        {
            Subject = new Subject { Type = "user", Id = caller.UserId },
            Action = new AuthZenAction { Name = "read" },
            Evaluations = candidates.Select(e => new EvaluationEntry
            {
                Resource = new Resource
                {
                    Type = "expense",
                    Id = e.Id.ToString(),
                    Properties = new Dictionary<string, object>
                    {
                        ["ownerId"] = e.OwnerUserId,
                        ["status"] = e.Status.ToString()
                    }
                }
            }).ToList()
        };

        var decisions = await pdp.AreAllowedAsync(batch, ct);
        return candidates.Where((_, i) => decisions[i]).ToList();
    }
}
