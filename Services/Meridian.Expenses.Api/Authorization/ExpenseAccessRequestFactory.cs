using System.Security.Claims;
using AuthZen.Contracts;
using Meridian.Services;

namespace Meridian.Expenses.Api.Authorization;

// Shared SARC-request builder for every call this API makes into the PDP —
// OwnerOrPrivilegedHandler, ApprovalHandler, and CreateExpensePdpFilter all
// need the same Subject/Resource("expense") skeleton and previously built it
// independently. Callers supply only what varies: the action, the resource
// id (null for Create, which has no persisted entity yet), the resource
// properties, and an optional context (e.g. the approval amount).
internal static class ExpenseAccessRequestFactory
{
    public static AccessEvaluationRequest Build(
        ClaimsPrincipal user,
        string action,
        string? resourceId,
        Dictionary<string, object> properties,
        Dictionary<string, object>? context = null) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = user.GetUserId() ?? string.Empty },
            Action = new AuthZenAction { Name = action },
            Resource = new Resource
            {
                Type = "expense",
                Id = resourceId,
                Properties = properties
            },
            Context = context
        };
}
