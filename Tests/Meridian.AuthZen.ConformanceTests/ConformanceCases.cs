using AuthZen.Contracts;
using Meridian.UnitTests.PdpService;

namespace Meridian.AuthZen.ConformanceTests;

// The shared decision matrix: every (resource type, action) pair the PDPs
// support, crossed with the seeded subjects (u-emma/u-mateo employees in
// Sales, u-nadia their Sales manager, u-finn finance, u-nobody unseeded) and
// the resource states each rule branches on. Keyed by a readable name so each
// case shows up as its own test in the runner.
public static class ConformanceCases
{
    // Thursday 2026-07-23 11:00 America/New_York (EDT) — inside business hours.
    private static readonly DateTimeOffset ThursdayMidday = new(2026, 7, 23, 15, 0, 0, TimeSpan.Zero);

    public static IReadOnlyDictionary<string, ConformanceCase> All { get; } = Build();

    private static Dictionary<string, ConformanceCase> Build()
    {
        var cases = new Dictionary<string, ConformanceCase>();

        void Add(string name, AccessEvaluationRequest request, bool expected, DateTimeOffset? now = null) =>
            cases.Add(name, new ConformanceCase(request, expected, now ?? ThursdayMidday));

        // ---- expense / create ----
        Add("expense.create employee own department", RequestFactory.ExpenseCreateRequest("u-emma", "u-emma", "Sales"), true);
        Add("expense.create department case-insensitive", RequestFactory.ExpenseCreateRequest("u-emma", "u-emma", "sales"), true);
        Add("expense.create finance own department", RequestFactory.ExpenseCreateRequest("u-finn", "u-finn", "Finance"), true);
        Add("expense.create on behalf of someone else", RequestFactory.ExpenseCreateRequest("u-emma", "u-mateo", "Sales"), false);
        Add("expense.create other department", RequestFactory.ExpenseCreateRequest("u-emma", "u-emma", "Finance"), false);
        Add("expense.create unknown subject", RequestFactory.ExpenseCreateRequest("u-nobody", "u-nobody", "Sales"), false);
        Add("expense.create missing department", Request("u-emma", "create", "expense", new() { ["ownerId"] = "u-emma" }), false);

        // ---- expense / read ----
        Add("expense.read owner draft", RequestFactory.ExpenseRequest("u-emma", "read", "u-emma", "Draft"), true);
        Add("expense.read unknown subject reading own", RequestFactory.ExpenseRequest("u-nobody", "read", "u-nobody", "Draft"), true);
        Add("expense.read finance draft", RequestFactory.ExpenseRequest("u-finn", "read", "u-emma", "Draft"), true);
        Add("expense.read manager of owner submitted", RequestFactory.ExpenseRequest("u-nadia", "read", "u-emma", "Submitted"), true);
        Add("expense.read manager of owner lowercase status", RequestFactory.ExpenseRequest("u-nadia", "read", "u-emma", "approved"), true);
        Add("expense.read manager of owner draft", RequestFactory.ExpenseRequest("u-nadia", "read", "u-emma", "Draft"), false);
        Add("expense.read manager of owner uppercase draft", RequestFactory.ExpenseRequest("u-nadia", "read", "u-emma", "DRAFT"), false);
        Add("expense.read manager of owner missing status", Request("u-nadia", "read", "expense", new() { ["ownerId"] = "u-emma" }), false);
        Add("expense.read manager not of owner", RequestFactory.ExpenseRequest("u-nadia", "read", "u-finn", "Submitted"), false);
        Add("expense.read peer employee", RequestFactory.ExpenseRequest("u-mateo", "read", "u-emma", "Submitted"), false);
        Add("expense.read unknown subject", RequestFactory.ExpenseRequest("u-nobody", "read", "u-emma", "Submitted"), false);

        // ---- expense / submit ----
        Add("expense.submit owner", RequestFactory.ExpenseRequest("u-emma", "submit", "u-emma", "Draft"), true);
        Add("expense.submit manager of owner", RequestFactory.ExpenseRequest("u-nadia", "submit", "u-emma", "Draft"), false);
        Add("expense.submit finance", RequestFactory.ExpenseRequest("u-finn", "submit", "u-emma", "Draft"), false);

        // ---- expense / approve ----
        Add("expense.approve manager under limit", RequestFactory.ExpenseRequest("u-nadia", "approve", "u-emma", "Submitted", 1200m), true);
        Add("expense.approve manager at limit", RequestFactory.ExpenseRequest("u-nadia", "approve", "u-emma", "Submitted", 5000m), true);
        Add("expense.approve manager just over limit", RequestFactory.ExpenseRequest("u-nadia", "approve", "u-emma", "Submitted", 5000.01m), false);
        Add("expense.approve manager missing amount", RequestFactory.ExpenseRequest("u-nadia", "approve", "u-emma", "Submitted"), false);
        Add("expense.approve manager draft", RequestFactory.ExpenseRequest("u-nadia", "approve", "u-emma", "Draft", 100m), false);
        Add("expense.approve manager not of owner", RequestFactory.ExpenseRequest("u-nadia", "approve", "u-finn", "Submitted", 100m), false);
        Add("expense.approve finance any amount", RequestFactory.ExpenseRequest("u-finn", "approve", "u-emma", "Submitted", 99999m), true);
        Add("expense.approve finance draft no amount", RequestFactory.ExpenseRequest("u-finn", "approve", "u-emma", "Draft"), true);
        Add("expense.approve owner self-approval", RequestFactory.ExpenseRequest("u-emma", "approve", "u-emma", "Submitted", 10m), false);
        Add("expense.approve peer employee", RequestFactory.ExpenseRequest("u-mateo", "approve", "u-emma", "Submitted", 10m), false);
        Add("expense.approve unknown subject", RequestFactory.ExpenseRequest("u-nobody", "approve", "u-emma", "Submitted", 10m), false);

        // ---- expense / reject ----
        Add("expense.reject manager under limit", RequestFactory.ExpenseRequest("u-nadia", "reject", "u-emma", "Submitted", 1200m), true);
        Add("expense.reject manager over limit", RequestFactory.ExpenseRequest("u-nadia", "reject", "u-emma", "Submitted", 6800m), false);
        Add("expense.reject finance", RequestFactory.ExpenseRequest("u-finn", "reject", "u-emma", "Submitted", 6800m), true);

        // ---- receipt / read ----
        Add("receipt.read owner", RequestFactory.ReceiptRequest("u-emma", "u-emma"), true);
        Add("receipt.read finance", RequestFactory.ReceiptRequest("u-finn", "u-emma"), true);
        Add("receipt.read manager of owner", RequestFactory.ReceiptRequest("u-nadia", "u-emma"), true);
        Add("receipt.read manager not of owner", RequestFactory.ReceiptRequest("u-nadia", "u-finn"), false);
        Add("receipt.read peer employee", RequestFactory.ReceiptRequest("u-mateo", "u-emma"), false);
        Add("receipt.read unknown subject", RequestFactory.ReceiptRequest("u-nobody", "u-emma"), false);

        // ---- receipt / create ----
        Add("receipt.create owner draft", RequestFactory.ReceiptCreateRequest("u-emma", "u-emma", "Draft"), true);
        Add("receipt.create owner lowercase draft", RequestFactory.ReceiptCreateRequest("u-emma", "u-emma", "draft"), true);
        Add("receipt.create owner submitted", RequestFactory.ReceiptCreateRequest("u-emma", "u-emma", "Submitted"), false);
        Add("receipt.create manager of owner", RequestFactory.ReceiptCreateRequest("u-nadia", "u-emma", "Draft"), false);
        Add("receipt.create finance", RequestFactory.ReceiptCreateRequest("u-finn", "u-emma", "Draft"), false);

        // ---- department_spend / read ----
        Add("department_spend.read finance", RequestFactory.DepartmentSpendRequest("u-finn", "read", "Sales"), true);
        Add("department_spend.read manager own department", RequestFactory.DepartmentSpendRequest("u-nadia", "read", "Sales"), true);
        Add("department_spend.read manager department case-sensitive", RequestFactory.DepartmentSpendRequest("u-nadia", "read", "sales"), false);
        Add("department_spend.read manager other department", RequestFactory.DepartmentSpendRequest("u-nadia", "read", "Finance"), false);
        Add("department_spend.read manager missing department", Request("u-nadia", "read", "department_spend", []), false);
        Add("department_spend.read employee", RequestFactory.DepartmentSpendRequest("u-emma", "read", "Sales"), false);
        Add("department_spend.read unknown subject", RequestFactory.DepartmentSpendRequest("u-nobody", "read", "Sales"), false);

        // ---- department_spend / export (business-hours window, America/New_York) ----
        var export = RequestFactory.DepartmentSpendRequest("u-finn", "export", "Sales");
        Add("department_spend.export finance weekday midday", export, true);
        Add("department_spend.export finance 08:59 EDT", export, false, new(2026, 7, 23, 12, 59, 0, TimeSpan.Zero));
        Add("department_spend.export finance 09:00 EDT", export, true, new(2026, 7, 23, 13, 0, 0, TimeSpan.Zero));
        Add("department_spend.export finance 16:59 EDT", export, true, new(2026, 7, 23, 20, 59, 0, TimeSpan.Zero));
        Add("department_spend.export finance 17:00 EDT", export, false, new(2026, 7, 23, 21, 0, 0, TimeSpan.Zero));
        Add("department_spend.export finance saturday", export, false, new(2026, 7, 25, 16, 0, 0, TimeSpan.Zero));
        Add("department_spend.export finance 08:30 EST", export, false, new(2026, 1, 15, 13, 30, 0, TimeSpan.Zero));
        Add("department_spend.export finance 09:30 EST", export, true, new(2026, 1, 15, 14, 30, 0, TimeSpan.Zero));
        Add("department_spend.export manager", RequestFactory.DepartmentSpendRequest("u-nadia", "export", "Sales"), false);
        Add("department_spend.export employee", RequestFactory.DepartmentSpendRequest("u-emma", "export", "Sales"), false);

        // ---- default deny on unmatched (resource type, action) ----
        Add("unknown action on expense", RequestFactory.ExpenseRequest("u-finn", "delete", "u-emma", "Submitted"), false);
        Add("unknown resource type", Request("u-finn", "read", "invoice", new() { ["ownerId"] = "u-finn" }), false);
        Add("unsupported action on receipt", Request("u-emma", "submit", "receipt", new() { ["ownerId"] = "u-emma" }), false);

        return cases;
    }

    private static AccessEvaluationRequest Request(
        string subjectId, string action, string resourceType, Dictionary<string, object> properties) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = action },
            Resource = new Resource { Type = resourceType, Id = "resource-1", Properties = properties }
        };
}
