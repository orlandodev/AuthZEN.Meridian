using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

public sealed class OwnerOrPrivilegedHandler
    : AuthorizationHandler<OwnerOrPrivilegedRequirement, ExpenseDto>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrPrivilegedRequirement requirement,
        ExpenseDto resource)
    {
        var isOwner   = context.User.GetUserId() == resource.OwnerUserId;
        var isFinance = context.User.IsInRole(Roles.Finance);
        var isManager = context.User.IsInRole(Roles.Manager)
                         && context.User.GetDepartment() == resource.Department;

        // The kind of bespoke, scattered rule that will move into the PDP later:
        if (isOwner || isFinance || isManager)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
