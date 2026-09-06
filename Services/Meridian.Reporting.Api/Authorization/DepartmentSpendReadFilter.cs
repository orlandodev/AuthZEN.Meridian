using AuthZen.Pep;

namespace Meridian.Reporting.Api.Authorization;

// The IEndpointFilter counterpart to Expenses.Api's CreateExpensePdpFilter:
// the department-spend list has no persisted resource to run through
// AuthorizationHandler<TRequirement, TResource>, so this builds the SARC
// request from the caller's own claims. Replaces the CanViewDepartmentSpend
// role policy — the manager-or-finance check, and the manager's own-department
// scoping, are now the PDP's call (DepartmentSpendRules.CanRead). The
// per-department row filtering still happens in ReportingService.
public sealed class DepartmentSpendReadFilter(IPolicyDecisionClient pdp) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = ReportingAccessRequestFactory.Build(context.HttpContext.User, "read");

        return await pdp.IsAllowedAsync(request)
            ? await next(context)
            : Results.Forbid();
    }
}
