using Meridian.Receipts.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ReceiptsApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ReceiptsApi.Authorization;

public class OwnerOrPrivilegedHandlerTests
{
    private readonly OwnerOrPrivilegedHandler _sut = new();

    private async Task<bool> SucceedsAsync(ClaimsPrincipal user, ReceiptDto resource)
    {
        var context = new AuthorizationHandlerContext([new OwnerOrPrivilegedRequirement()], user, resource);
        await _sut.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Owner_Succeeds_EvenWithoutAPrivilegedRole()
    {
        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, receipt)).Should().BeTrue();
    }

    [Fact]
    public async Task Finance_Succeeds_RegardlessOfOwnership()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Finance);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, receipt)).Should().BeTrue();
    }

    // Completes the {employee, manager, finance} x {own, others'} matrix: a manager
    // viewing their own receipt succeeds via the isOwner branch, same as any employee.
    [Fact]
    public async Task Manager_Succeeds_WhenViewingTheirOwnReceipt()
    {
        var user = BuildUser(userId: OwnerUserId, role: Roles.Manager, department: Department);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, receipt)).Should().BeTrue();
    }

    // Completes the matrix: finance viewing their own receipt succeeds — either
    // branch (isOwner or isFinance) alone is sufficient.
    [Fact]
    public async Task Finance_Succeeds_WhenViewingTheirOwnReceipt()
    {
        var user = BuildUser(userId: OwnerUserId, role: Roles.Finance);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, receipt)).Should().BeTrue();
    }

    [Fact]
    public async Task NonOwnerEmployee_Fails()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Employee, department: Department);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, receipt)).Should().BeFalse();
    }

    // Pins the Stage 1 drift bug: a manager whose claims would satisfy Expenses.Api's
    // OwnerOrPrivilegedHandler (same department as the resource) still fails here,
    // because ReceiptDto has no Department to check against. This is the executable
    // proof that the drift is real and intentional, not a typo.
    [Fact]
    public async Task Manager_Fails_EvenThoughTheEquivalentExpensesApiCheckWouldSucceed()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, receipt)).Should().BeFalse();
    }
}
