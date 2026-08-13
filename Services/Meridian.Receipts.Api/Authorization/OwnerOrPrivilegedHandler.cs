using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Receipts.Api.Authorization;

public sealed class OwnerOrPrivilegedHandler
    : AuthorizationHandler<OwnerOrPrivilegedRequirement, ReceiptDto>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrPrivilegedRequirement requirement,
        ReceiptDto resource)
    {
        var isOwner = context.User.GetUserId() == resource.OwnerUserId;
        var isFinance = context.User.IsInRole(Roles.Finance);

        // Stage 1 (deliberate drift): Expenses.Api's OwnerOrPrivilegedHandler also
        // allows a manager in the same department — never copied here, and
        // structurally can't be, since Receipt has no Department field. A manager
        // who can open the Expense via Expenses.Api gets 403 here on its Receipt.
        if (isOwner || isFinance)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
