using AuthZen.Contracts;

namespace Meridian.UnitTests.PdpService;

// Concise builders for SARC requests used across RulesEngineTests.
public static class RequestFactory
{
    public static AccessEvaluationRequest ExpenseRequest(
        string subjectId,
        string action,
        string ownerId,
        string status,
        decimal? contextAmount = null) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = action },
            Resource = new Resource
            {
                Type = "expense",
                Id = "expense-1",
                Properties = new Dictionary<string, object>
                {
                    ["ownerId"] = ownerId,
                    ["status"] = status
                }
            },
            Context = contextAmount is null
                ? null
                : new Dictionary<string, object> { ["amount"] = contextAmount.Value }
        };

    public static AccessEvaluationRequest ExpenseCreateRequest(string subjectId, string ownerId, string department) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = "create" },
            Resource = new Resource
            {
                Type = "expense",
                Properties = new Dictionary<string, object>
                {
                    ["ownerId"] = ownerId,
                    ["department"] = department
                }
            }
        };

    public static AccessEvaluationRequest ReceiptRequest(string subjectId, string ownerId) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = "read" },
            Resource = new Resource
            {
                Type = "receipt",
                Id = "receipt-1",
                Properties = new Dictionary<string, object> { ["ownerId"] = ownerId }
            }
        };

    // No resource Id: mirrors UploadEligibilityHandler, which authorizes an
    // upload against the parent expense's owner/status before any Receipt exists.
    public static AccessEvaluationRequest ReceiptCreateRequest(string subjectId, string ownerId, string status) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = "create" },
            Resource = new Resource
            {
                Type = "receipt",
                Properties = new Dictionary<string, object>
                {
                    ["ownerId"] = ownerId,
                    ["status"] = status
                }
            }
        };

    public static AccessEvaluationRequest DepartmentSpendRequest(string subjectId, string action, string department) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = action },
            Resource = new Resource
            {
                Type = "department_spend",
                Id = "2025-01",
                Properties = new Dictionary<string, object> { ["department"] = department }
            }
        };
}
