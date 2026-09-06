using AuthZen.Pep;

namespace Meridian.Reporting.Api.Authorization;

// Replaces both the CanExportDepartmentSpend role policy and the in-process
// BusinessHoursPolicy branch that used to sit inside the export handler: a
// single ("department_spend", "export") evaluation now covers finance-only
// access and the Monday-Friday 9am-5pm UTC window together
// (DepartmentSpendRules.CanExport). The time itself is the PDP's own — it is
// never carried in the request — so a PEP cannot widen the window by lying
// about the clock. A denial collapses to one 403 regardless of which half
// failed; the Portal shows its business-hours message for any 403 here.
public sealed class DepartmentSpendExportFilter(IPolicyDecisionClient pdp) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = ReportingAccessRequestFactory.Build(context.HttpContext.User, "export");

        return await pdp.IsAllowedAsync(request)
            ? await next(context)
            : Results.Forbid();
    }
}
