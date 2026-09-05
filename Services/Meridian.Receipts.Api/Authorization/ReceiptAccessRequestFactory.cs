using System.Security.Claims;
using AuthZen.Contracts;
using Meridian.Services;

namespace Meridian.Receipts.Api.Authorization;

// Shared SARC-request builder for every call this API makes into the PDP —
// OwnerOrPrivilegedHandler and UploadEligibilityHandler both need the same
// Subject/Resource("receipt") skeleton and would otherwise build it
// independently. Mirrors Expenses.Api's ExpenseAccessRequestFactory. Callers
// supply only what varies: the action, the resource id (null for the
// upload/create check, which has no persisted Receipt yet), and the resource
// properties.
internal static class ReceiptAccessRequestFactory
{
    public static AccessEvaluationRequest Build(
        ClaimsPrincipal user,
        string action,
        string? resourceId,
        Dictionary<string, object> properties) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = user.GetUserId() ?? string.Empty },
            Action = new AuthZenAction { Name = action },
            Resource = new Resource
            {
                Type = "receipt",
                Id = resourceId,
                Properties = properties
            }
        };
}
