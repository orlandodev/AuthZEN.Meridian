using AuthZen.Pep;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Receipts.Api.Authorization;

// Delegates the owner+Draft upload check to the PDP as ("receipt", "create")
// instead of enforcing in-process. The SARC resource type is "receipt" — the
// thing the caller is asking permission to create — but a receipt upload has
// no persisted Receipt yet (and ReceiptDto itself has no Status), so the
// properties that inform the decision (ownerId, status) deliberately describe
// the parent Expense (fetched from Expenses.Api via ExpensesLookupClient)
// instead. This is intentionally asymmetric with Expenses.Api's
// CreateExpensePdpFilter, whose "expense" resource properties describe the new
// expense itself.
public sealed class UploadEligibilityHandler(IPolicyDecisionClient pdp)
    : AuthorizationHandler<UploadEligibilityRequirement, ExpenseDto>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UploadEligibilityRequirement requirement,
        ExpenseDto resource)
    {
        var request = ReceiptAccessRequestFactory.Build(
            context.User,
            "create",
            resourceId: null,
            new Dictionary<string, object>
            {
                ["ownerId"] = resource.OwnerUserId,
                ["status"] = resource.Status.ToString()
            });

        if (await pdp.IsAllowedAsync(request))
        {
            context.Succeed(requirement);
        }
    }
}
