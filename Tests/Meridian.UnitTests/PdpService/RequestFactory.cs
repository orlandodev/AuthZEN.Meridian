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
