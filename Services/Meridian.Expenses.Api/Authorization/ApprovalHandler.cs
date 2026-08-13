using AuthZen.Pep;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

// Stage 3: delegates the approve/reject decision to the PDP instead of
// enforcing in-process. The amount limit that used to live here as
// ApprovalRules.ManagerLimit now lives in the PDP's policy database
// (PolicyConstants.AmountLimitKeys.ExpenseApproveManagerLimit).
public sealed class ApprovalHandler(IPolicyDecisionClient pdp)
    : AuthorizationHandler<ApprovalRequirement, ExpenseDto>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovalRequirement requirement,
        ExpenseDto resource)
    {
        var actionName = requirement.DesiredStatus switch
        {
            ExpenseStatus.Approved => "approve",
            ExpenseStatus.Rejected => "reject",
            _ => throw new ArgumentOutOfRangeException(
                nameof(requirement), requirement.DesiredStatus, "Unsupported approval decision.")
        };

        var request = ExpenseAccessRequestFactory.Build(
            context.User,
            actionName,
            resource.Id.ToString(),
            new Dictionary<string, object>
            {
                ["ownerId"] = resource.OwnerUserId,
                ["status"] = resource.Status.ToString()
            },
            new Dictionary<string, object> { ["amount"] = resource.Amount });

        if (await pdp.IsAllowedAsync(request))
        {
            context.Succeed(requirement);
        }
    }
}
