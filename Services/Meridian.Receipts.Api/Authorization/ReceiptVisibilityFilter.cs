using System.Security.Claims;
using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Services;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;

namespace Meridian.Receipts.Api.Authorization;

// Narrows IReceiptService.GetForExpenseAsync's manager-branch candidates
// (every receipt on the expense — the same broad set Finance sees) down to
// the same set OwnerOrPrivilegedHandler would allow one-by-one via the PDP's
// "read" rule — a genuine ManagerOf relationship, not just holding the
// Manager role — so the list and download endpoints never disagree about
// what a manager can see. Mirrors Expenses.Api's ExpenseVisibilityFilter.
// Finance (unconditional in both ReceiptService and ReceiptRules.CanRead)
// and non-manager (owner-only, trivially symmetric with IsOwner) candidate
// sets pass through unfiltered; only the manager branch can diverge from
// ReceiptRules.CanRead, so only it needs the extra PDP round trip.
public sealed class ReceiptVisibilityFilter(IReceiptService receipts, IPolicyDecisionClient pdp)
{
    public async Task<IReadOnlyList<ReceiptDto>> GetVisibleReceiptsAsync(
        Guid expenseId, ClaimsPrincipal user, CancellationToken ct)
    {
        var caller = user.ToCallerContext();
        var candidates = await receipts.GetForExpenseAsync(expenseId, caller, ct);

        if (!caller.IsManager || caller.IsFinance || candidates.Count == 0)
        {
            return candidates;
        }

        var batch = new AccessEvaluationsRequest
        {
            Subject = new Subject { Type = "user", Id = caller.UserId },
            Action = new AuthZenAction { Name = "read" },
            Evaluations = candidates.Select(r => new EvaluationEntry
            {
                Resource = new Resource
                {
                    Type = "receipt",
                    Id = r.Id.ToString(),
                    Properties = new Dictionary<string, object> { ["ownerId"] = r.OwnerUserId }
                }
            }).ToList()
        };

        var decisions = await pdp.AreAllowedAsync(batch, ct);
        return candidates.Where((_, i) => decisions[i]).ToList();
    }
}
