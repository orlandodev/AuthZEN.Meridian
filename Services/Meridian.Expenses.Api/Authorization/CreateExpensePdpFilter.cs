using AuthZen.Pep;
using Meridian.Services;

namespace Meridian.Expenses.Api.Authorization;

// The IEndpointFilter counterpart to OwnerOrPrivilegedHandler/ApprovalHandler:
// Create has no persisted entity to run through
// AuthorizationHandler<TRequirement, TResource>, so this builds the SARC
// request from the caller's own claims instead. CreateExpenseRequest carries
// neither ownerId nor department, so nothing here reads the request body.
public sealed class CreateExpensePdpFilter(IPolicyDecisionClient pdp) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        var department = user.GetDepartment();
        if (department is null)
        {
            return Results.BadRequest("User has no department claim.");
        }

        var userId = user.GetUserId() ?? string.Empty;
        var request = ExpenseAccessRequestFactory.Build(
            user,
            "create",
            resourceId: null,
            new Dictionary<string, object>
            {
                ["ownerId"] = userId,
                ["department"] = department
            });

        return await pdp.IsAllowedAsync(request)
            ? await next(context)
            : Results.Forbid();
    }
}
