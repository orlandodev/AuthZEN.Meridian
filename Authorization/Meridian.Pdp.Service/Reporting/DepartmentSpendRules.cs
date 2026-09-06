using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Reporting;

// Replicates Meridian.Reporting.Api's CanViewDepartmentSpend (manager or
// finance, department-scoped in ReportingService.GetDepartmentSpendAsync)
// and CanExportDepartmentSpend (finance only, additionally gated by
// BusinessHoursPolicy in ReportingEndpoints.cs — replicated here too, in
// that same order: role first, then the time-of-day gate).
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

        return BusinessHoursPolicy.IsWithinBusinessHours(ws.TimeProvider);
    }
}
