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
        // allows a manager in the same department as the resource. That rule was
        // never copied here — and structurally *can't* be, because Receipt (unlike
        // Expense) has no Department field. This is the "pain" of Stage 1: the same
        // conceptual rule ("owner, finance, or the resource's manager") now has two
        // divergent implementations across services, and this one is missing a case.
        // A manager who can open their department's Expense via Expenses.Api will
        // get 403 here when trying to view/download the associated Receipt.
        if (isOwner || isFinance)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
