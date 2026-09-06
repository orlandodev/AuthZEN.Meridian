using System.Security.Claims;
using AuthZen.Contracts;
using Meridian.Services;

namespace Meridian.Reporting.Api.Authorization;

// Shared SARC-request builder for every call this API makes into the PDP —
// DepartmentSpendReadFilter and DepartmentSpendExportFilter both need the same
// Subject/Resource("department_spend") skeleton and would otherwise build it
// independently. Mirrors Expenses.Api's ExpenseAccessRequestFactory. Neither
// call targets a single persisted summary, so there is no resource id; the
// caller's own department rides along as a property because the PDP's
// department-scoping rule reads it for managers.
internal static class ReportingAccessRequestFactory
{
    public static AccessEvaluationRequest Build(ClaimsPrincipal user, string action) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = user.GetUserId() ?? string.Empty },
            Action = new AuthZenAction { Name = action },
            Resource = new Resource
            {
                Type = "department_spend",
                Properties = new Dictionary<string, object>
                {
                    ["department"] = user.GetDepartment() ?? string.Empty
                }
            }
        };
}
