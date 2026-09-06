using AuthZen.Pep;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

// Delegates the ownership/read decision to the PDP instead of enforcing
// in-process. The department-vs-manager scoping and the Draft
// carve-out that used to live here now live in the PDP's ExpenseRules.CanRead,
// backed by the org chart in PolicyDbContext rather than a claims comparison.
public sealed class OwnerOrPrivilegedHandler(IPolicyDecisionClient pdp)
    : AuthorizationHandler<OwnerOrPrivilegedRequirement, ExpenseDto>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrPrivilegedRequirement requirement,
        ExpenseDto resource)
    {
        var request = ExpenseAccessRequestFactory.Build(
            context.User,
            "read",
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
