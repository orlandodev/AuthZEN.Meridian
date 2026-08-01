using System.Security.Claims;
using Meridian.DataAccess.Models;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using ExpensesAuth = Meridian.Expenses.Api.Authorization;
using ReceiptsAuth = Meridian.Receipts.Api.Authorization;

namespace Meridian.UnitTests;

// Story 1.2's whole point: OwnerOrPrivilegedHandler was copy-pasted from
// Expenses.Api into Receipts.Api and the same-department manager clause was
// dropped in the copy. This test proves the drift concretely — the exact same
// caller claims, evaluated against the "same" resource (same owner, same
// department) by each service's own handler, produce different outcomes.
public class AuthorizationDriftTests
{
    private const string OwnerUserId = "u-emma";
    private const string ManagerUserId = "u-nadia";
    private const string Department = "Sales";

    private static ClaimsPrincipal BuildManager() => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, ManagerUserId),
            new Claim(ClaimTypes.Role, Roles.Manager),
            new Claim("department", Department)
        ], "TestAuth"));

    [Fact]
    public async Task SameManagerAndOwner_SucceedsInExpensesApi_ButFailsInReceiptsApi()
    {
        var manager = BuildManager();

        var expense = new ExpenseDto(
            Id: Guid.NewGuid(), OwnerUserId: OwnerUserId, Department: Department,
            Amount: 100m, Currency: "USD", Category: "Meals", Status: ExpenseStatus.Submitted,
            ApproverUserId: null, CreatedAt: DateTimeOffset.UtcNow, DecidedAt: null);

        var receipt = new ReceiptDto(
            Id: Guid.NewGuid(), ExpenseId: expense.Id, OwnerUserId: OwnerUserId,
            FileName: "receipt.jpg", ContentType: "image/jpeg", UploadedAt: DateTimeOffset.UtcNow);

        var expensesContext = new AuthorizationHandlerContext(
            [new ExpensesAuth.OwnerOrPrivilegedRequirement()], manager, expense);
        await new ExpensesAuth.OwnerOrPrivilegedHandler().HandleAsync(expensesContext);

        var receiptsContext = new AuthorizationHandlerContext(
            [new ReceiptsAuth.OwnerOrPrivilegedRequirement()], manager, receipt);
        await new ReceiptsAuth.OwnerOrPrivilegedHandler().HandleAsync(receiptsContext);

        expensesContext.HasSucceeded.Should().BeTrue(
            "the manager shares the expense's department, which Expenses.Api's handler allows");
        receiptsContext.HasSucceeded.Should().BeFalse(
            "Receipts.Api's handler never copied the department-based manager check " +
            "(and ReceiptDto has no Department field to check against) — this is the Stage 1 drift");
    }
}
