using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

public sealed class ApprovalHandler : AuthorizationHandler<ApprovalRequirement, ExpenseDto>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovalRequirement requirement,
        ExpenseDto resource)
    {
        if (context.User.IsInRole(Roles.Finance))
        {
            context.Succeed(requirement);   // no limit, no department restriction
        }
        else if (context.User.IsInRole(Roles.Manager)
                 && context.User.GetDepartment() == resource.Department
                 && resource.Amount <= ApprovalRules.ManagerLimit)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
