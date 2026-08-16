using Meridian.DataAccess.Models;
using Meridian.Receipts.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ReceiptsApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ReceiptsApi.Authorization;

public class UploadEligibilityHandlerTests
{
    private readonly UploadEligibilityHandler _sut = new();

    private async Task<bool> SucceedsAsync(ClaimsPrincipal user, ExpenseDto resource)
    {
        var context = new AuthorizationHandlerContext([new UploadEligibilityRequirement()], user, resource);
        await _sut.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Owner_OnDraftExpense_Succeeds()
    {
        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Draft);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Owner_OnSubmittedExpense_Fails()
    {
        // Owner-only isn't enough on its own — upload closes once the expense
        // leaves Draft, even for its own owner.
        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Submitted);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    [Fact]
    public async Task NonOwnerEmployee_OnDraftExpense_Fails()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Employee, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Draft);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    // Unlike OwnerOrPrivilegedRequirement, Finance gets no carve-out here —
    // Manager and Finance can never upload, at any status, per Story 4.0.
    [Fact]
    public async Task Finance_OnDraftExpense_Fails()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Finance);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Draft);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }
}
