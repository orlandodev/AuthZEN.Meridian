using AuthZen.Pep;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Receipts.Api.Authorization;

// Delegates the ownership/read decision to the PDP instead of enforcing
// in-process. The manager-of branch this API's own check never had (see
// AuthorizationPrimitives.cs — Receipt has no Department field to key off) now
// applies here too, via ReceiptRules.CanRead.
public sealed class OwnerOrPrivilegedHandler(IPolicyDecisionClient pdp)
    : AuthorizationHandler<OwnerOrPrivilegedRequirement, ReceiptDto>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrPrivilegedRequirement requirement,
        ReceiptDto resource)
    {
        var request = ReceiptAccessRequestFactory.Build(
            context.User,
            "read",
            resource.Id.ToString(),
            new Dictionary<string, object> { ["ownerId"] = resource.OwnerUserId });

        if (await pdp.IsAllowedAsync(request))
        {
            context.Succeed(requirement);
        }
    }
}
