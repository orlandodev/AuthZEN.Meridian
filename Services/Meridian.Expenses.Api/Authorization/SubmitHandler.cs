using AuthZen.Pep;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

// Stage 3/Story 4.0: delegates the Submit owner-check to the PDP, the same way
// OwnerOrPrivilegedHandler and ApprovalHandler delegate read/decide — keeps
// every expense-lifecycle authorization decision in one place (ExpenseRules)
// instead of splitting it between the PDP and inline endpoint checks.
public sealed class SubmitHandler(IPolicyDecisionClient pdp) : AuthorizationHandler<SubmitRequirement, ExpenseDto>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SubmitRequirement requirement,
        ExpenseDto resource)
    {
        var request = ExpenseAccessRequestFactory.Build(
            context.User,
            "submit",
            resource.Id.ToString(),
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
