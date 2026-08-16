using Meridian.DataAccess.Models;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Receipts.Api.Authorization;

// Resource is the parent Expense (fetched from Expenses.Api via
// ExpensesLookupClient), not a Receipt — receipt upload eligibility depends on
// the expense's owner and status, neither of which a Receipt carries.
public sealed class UploadEligibilityHandler
    : AuthorizationHandler<UploadEligibilityRequirement, ExpenseDto>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UploadEligibilityRequirement requirement,
        ExpenseDto resource)
    {
        if (context.User.GetUserId() == resource.OwnerUserId && resource.Status == ExpenseStatus.Draft)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
