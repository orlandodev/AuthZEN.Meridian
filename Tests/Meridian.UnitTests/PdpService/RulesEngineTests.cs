using Meridian.DataAccess.Models;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;
using Meridian.UnitTests.TestSupport;

namespace Meridian.UnitTests.PdpService;

public class RulesEngineTests
{
    // Program.cs resolves this from BusinessHours:TimeZone; PolicyRulesEngine
    // has no fallback, so the export cases pass it explicitly.
    private static readonly TimeZoneInfo BusinessZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    // 2026-07-23 15:00 UTC is a Thursday, 11:00 in America/New_York (EDT) —
    // inside the Mon-Fri 9am-5pm business-zone window.
    private static readonly TimeProvider WithinBusinessHours =
        new FakeTimeProvider(new DateTimeOffset(2026, 7, 23, 15, 0, 0, TimeSpan.Zero));

    // 2026-07-25 is a Saturday.
    private static readonly TimeProvider OutsideBusinessHours =
        new FakeTimeProvider(new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));


    // ---- expense / create ----

    [Theory]
    [InlineData("u-emma", "Sales")]   // employee
    [InlineData("u-nadia", "Sales")]  // manager
    [InlineData("u-finn", "Finance")] // finance
    public async Task Expense_Create_OwnerAndDepartmentMatchCaller_Allowed(string subjectId, string department)
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseCreateRequest(subjectId, ownerId: subjectId, department: department);

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Create_OwnerIdDoesNotMatchSubject_Denied()
    {
        // Defense in depth: a PEP that ever lets a caller assert someone
        // else's ownerId (e.g. a future "create on behalf of" bug) must
        // still be denied here, not just trusted.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseCreateRequest("u-emma", ownerId: "u-mateo", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Create_DepartmentDoesNotMatchSubjectsOwnDepartment_Denied()
    {
        // u-emma is Sales; claiming Finance must be denied even though the
        // ownerId is correct.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseCreateRequest("u-emma", ownerId: "u-emma", department: "Finance");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Create_UnknownSubject_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseCreateRequest("u-ghost", ownerId: "u-ghost", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

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
        // Draft carve-out overrides even a genuine manager-of relationship.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-nadia", "read", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Expense_Read_ManagerOfOwner_MissingStatus_Denied()
    {
        // Fail closed: a PEP that omits "status" must be denied, not
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
    public async Task Expense_Read_SameDepartmentManagerButNotManagerOf_Denied()
    {
        // Isolates department from ManagerOf, unlike the test above (u-finn
        // differs on both). CanRead never reads a "department" property at
        // all — it's ManagerOf-only — so a second manager who shares u-emma's
        // department but has no ManagerOf row must still be denied here, even
        // though Meridian.Services.ExpenseService.GetVisibleExpensesAsync
        // (Expenses.Api's list endpoint) would return u-emma's expense to
        // this same caller purely on department match. That divergence
        // between list and detail is real: see ExpenseServiceTests.
        // GetVisibleExpensesAsync_ReturnsDepartmentExpenses_ForManagerCaller,
        // which proves the list side ignores ManagerOf entirely.
        using var db = PolicyDbContextTestFactory.Create();
        db.RoleAssignments.Add(new RoleAssignment
        {
            UserId = "u-priya", Role = PolicyConstants.RoleNames.Manager, Department = "Sales"
        });
        await db.SaveChangesAsync();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-priya", "read", ownerId: "u-emma", status: "Submitted");

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

    // ---- expense / submit ----

    [Fact]
    public async Task Expense_Submit_Owner_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-emma", "submit", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Expense_Submit_NonOwner_Denied()
    {
        // Owner-only, full stop — unlike CanRead, Finance and a manager-of the
        // owner get no carve-out here.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ExpenseRequest("u-finn", "submit", ownerId: "u-emma", status: "Draft");

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
        // Proves the PDP intentionally normalizes past Receipts.Api's own
        // check, which has no manager-of branch there today.
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

    [Fact]
    public async Task Receipt_Read_ManagerNotManagerOf_Denied()
    {
        // A genuine Manager role isn't enough on its own — u-nadia only
        // manages u-emma and u-mateo, not u-finn — mirrors
        // Expense_Read_ManagerNotManagerOf_Denied above so the manager-of
        // branch's negative case is covered for receipts too, not just its
        // positive case (Receipt_Read_ManagerOfOwner_Allowed).
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptRequest("u-nadia", ownerId: "u-finn");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    // ---- receipt / create ----

    [Fact]
    public async Task Receipt_Create_Owner_Draft_Allowed()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptCreateRequest("u-emma", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task Receipt_Create_Owner_Submitted_Denied()
    {
        // Upload closes once the expense leaves Draft, even for its own owner —
        // narrower than Receipt_Read, which has no status gate at all.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptCreateRequest("u-emma", ownerId: "u-emma", status: "Submitted");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Receipt_Create_NonOwnerEmployee_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptCreateRequest("u-mateo", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Receipt_Create_Finance_Denied()
    {
        // Unlike Receipt_Read, Finance gets no carve-out — only the literal
        // owner may ever upload.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptCreateRequest("u-finn", ownerId: "u-emma", status: "Draft");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task Receipt_Create_ManagerOfOwner_Denied()
    {
        // Unlike Receipt_Read, a manager-of relationship gives no carve-out either.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db);

        var request = RequestFactory.ReceiptCreateRequest("u-nadia", ownerId: "u-emma", status: "Draft");

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
        var engine = new PolicyRulesEngine(db, WithinBusinessHours, BusinessZone);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeTrue();
    }

    [Fact]
    public async Task DepartmentSpend_Export_Finance_OutsideBusinessHours_Denied()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db, OutsideBusinessHours, BusinessZone);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task DepartmentSpend_Export_Manager_Denied()
    {
        // Even for their own department, and even within business hours — export is finance-only.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db, WithinBusinessHours, BusinessZone);

        var request = RequestFactory.DepartmentSpendRequest("u-nadia", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Fact]
    public async Task DepartmentSpend_Export_Finance_InsideUtcWindowButBeforeNineInBusinessZone_Denied()
    {
        // 12:00 UTC would be inside a 9-5 *UTC* window, but it is only 08:00 in
        // America/New_York (EDT) — the rule checks the business zone, not UTC.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(
            db, new FakeTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)), BusinessZone);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().BeFalse();
    }

    [Theory]
    [InlineData(2026, 1, 22, true)]   // 21:30 UTC is 16:30 EST — winter, still inside
    [InlineData(2026, 7, 23, false)]  // 21:30 UTC is 17:30 EDT — summer, past close
    public async Task DepartmentSpend_Export_Finance_WindowTracksBusinessZoneDst(
        int year, int month, int day, bool expected)
    {
        // Same UTC time-of-day, opposite decisions: the close is 17:00 in the
        // business zone, and that zone's offset shifts with DST.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(
            db, new FakeTimeProvider(new DateTimeOffset(year, month, day, 21, 30, 0, TimeSpan.Zero)), BusinessZone);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");

        (await engine.EvaluateAsync(request)).Should().Be(expected);
    }

    [Fact]
    public async Task DepartmentSpend_Export_NoBusinessTimeZone_Throws()
    {
        // There is no fallback zone: an engine built without one (only reachable
        // by mis-wiring — Program.cs requires BusinessHours:TimeZone) fails loudly
        // rather than silently picking a default.
        using var db = PolicyDbContextTestFactory.Create();
        var engine = new PolicyRulesEngine(db, WithinBusinessHours);

        var request = RequestFactory.DepartmentSpendRequest("u-finn", "export", department: "Sales");
        var act = async () => await engine.EvaluateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
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
