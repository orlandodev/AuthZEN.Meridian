using Meridian.Pdp.Service.Pdp;
using Meridian.UnitTests.TestSupport;

namespace Meridian.UnitTests.PdpService;

public class RulesEngineTests
{
    // 2026-07-23 is a Thursday, within business hours (9am-5pm UTC Mon-Fri).
    private static readonly TimeProvider WithinBusinessHours =
        new FakeTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));

    // 2026-07-25 is a Saturday.
    private static readonly TimeProvider OutsideBusinessHours =
        new FakeTimeProvider(new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));


    // ---- expense / read ----

    [Fact]
    public async Task Expense_Read_Owner_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Read_Finance_AlwaysAllowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-finn", "read", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Read_ManagerOfOwner_Submitted_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "read", ownerId: "u-emma", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Read_ManagerOfOwner_Draft_Denied()
    {
        // The critical Draft carve-out regression test: preserved exactly
        // even for a manager who genuinely manages the owner.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "read", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Read_ManagerOfOwner_MissingStatus_Denied()
    {
        // Regression: a PEP that omits "status" must fail closed, not be
        // treated as equivalent to "not Draft".
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "read", ownerId: "u-emma", status: "Submitted") with
        {
            Resource = new AuthZen.Contracts.Resource
            {
                Type = "expense",
                Id = "expense-1",
                Properties = new Dictionary<string, object> { ["ownerId"] = "u-emma" }
            }
        };

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Read_ManagerNotManagerOf_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        // u-nadia only manages u-emma and u-mateo, not u-finn.
        var request = RequestFactory.ExpenseRequest("u-nadia", "read", ownerId: "u-finn", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Read_Stranger_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-mateo", "read", ownerId: "u-emma", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    // ---- expense / approve, reject ----

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Expense_Decide_Finance_UnconditionalAnyAmount_Allowed(string action)
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-finn", action, ownerId: "u-emma", status: "Submitted", contextAmount: 999_999m);

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Decide_ManagerUnderLimit_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 4000m);

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Decide_ManagerAtExactLimit_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 5000m);

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Decide_ManagerOverLimit_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 5001m);

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Decide_ManagerNotManagerOf_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-finn", status: "Submitted", contextAmount: 100m);

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task Expense_Decide_NonSubmittedStatus_Denied(string status)
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: status, contextAmount: 100m);

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Decide_MissingAmountInContext_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: null);

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Decide_Stranger_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-mateo", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 100m);

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    // ---- receipt / read ----

    [Fact]
    public async Task Receipt_Read_Owner_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptRequest("u-emma", ownerId: "u-emma");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Receipt_Read_Finance_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptRequest("u-finn", ownerId: "u-emma");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Receipt_Read_ManagerOfOwner_Allowed()
    {
        // Proves the PDP intentionally normalizes past Receipts.Api's
        // documented Stage-1 drift (no manager branch there today).
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptRequest("u-nadia", ownerId: "u-emma");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Receipt_Read_Stranger_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptRequest("u-mateo", ownerId: "u-emma");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    // ---- department_spend / read, export ----

    [Fact]
    public async Task DepartmentSpend_Read_Finance_AnyDepartment_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "read", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task DepartmentSpend_Read_ManagerOwnDepartment_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.DepartmentSpendRequest("u-nadia", "read", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task DepartmentSpend_Read_ManagerOtherDepartment_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.DepartmentSpendRequest("u-nadia", "read", department: "Finance");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task DepartmentSpend_Export_Finance_WithinBusinessHours_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db, WithinBusinessHours);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task DepartmentSpend_Export_Finance_OutsideBusinessHours_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db, OutsideBusinessHours);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task DepartmentSpend_Export_Manager_Denied()
    {
        // Even for their own department, and even within business hours — export is finance-only.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db, WithinBusinessHours);

        var request = RequestFactory.DepartmentSpendRequest("u-nadia", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    // ---- default-deny ----

    [Fact]
    public async Task UnknownResourceType_DefaultDeny()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Submitted") with
        {
            Resource = new AuthZen.Contracts.Resource { Type = "widget", Id = "1" }
        };

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownAction_DefaultDeny()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-finn", "delete", ownerId: "u-emma", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownSubject_ReadingSomeoneElsesResource_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-ghost", "read", ownerId: "u-emma", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownSubject_ReadingOwnResource_Allowed()
    {
        // Documents the intentional design point: ownership bypasses the
        // role lookup entirely — the PDP trusts *which* subject is being
        // asked about, and only centralizes *what they're allowed to do*.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-ghost", "read", ownerId: "u-ghost", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }
}
