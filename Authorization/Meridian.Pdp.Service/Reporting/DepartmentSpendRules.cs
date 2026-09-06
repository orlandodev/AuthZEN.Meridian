using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Reporting;

// The department-spend read and export decisions Reporting.Api delegates here
// (see DepartmentSpendReadFilter / DepartmentSpendExportFilter). Read is
// manager-or-finance, department-scoped for managers; the per-row scoping
// still runs in ReportingService.GetDepartmentSpendAsync. Export is
// finance-only, then the business-hours gate — role first, time-of-day second.
public static class DepartmentSpendRules
{
    public static async Task<bool> CanRead(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct)
    {
        var subject = await ws.GetProfileAsync(request.Subject.Id, ct);
        if (subject is null)
        {
            return false;
        }

        if (subject.Role == PolicyConstants.RoleNames.Finance)
        {
            return true;
        }

        if (subject.Role != PolicyConstants.RoleNames.Manager)
        {
            return false;
        }

        var resourceDept = RulePrimitives.GetResourceDepartment(request);
        return resourceDept is not null
            && string.Equals(subject.Department, resourceDept, StringComparison.Ordinal);
    }

    public static async Task<bool> CanExport(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct)
    {
        var subject = await ws.GetProfileAsync(request.Subject.Id, ct);
        if (subject?.Role != PolicyConstants.RoleNames.Finance)
        {
            return false;
        }

        var businessTimeZone = ws.BusinessTimeZone
            ?? throw new InvalidOperationException(
                "No business timezone configured; set BusinessHours:TimeZone (see Pdp.Service Program.cs).");
        return BusinessHoursPolicy.IsWithinBusinessHours(ws.TimeProvider, businessTimeZone);
    }
}
