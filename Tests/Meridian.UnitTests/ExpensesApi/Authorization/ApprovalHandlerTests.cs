using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ExpensesApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ExpensesApi.Authorization;

public class ApprovalHandlerTests
{
    private const decimal UnderLimit = ApprovalRules.ManagerLimit - 1m;
    private const decimal OverLimit = ApprovalRules.ManagerLimit + 1m;

    private readonly ApprovalHandler _sut = new();

    private async Task<bool> SucceedsAsync(ClaimsPrincipal user, ExpenseDto resource)
    {
        var context = new AuthorizationHandlerContext([new ApprovalRequirement()], user, resource);
        await _sut.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Finance_Succeeds_RegardlessOfAmountOrDepartment()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Finance, department: OtherDepartment);
        var expense = BuildExpense(department: Department, amount: OverLimit);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Manager_Succeeds_WhenDepartmentMatchesAndUnderLimit()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(department: Department, amount: UnderLimit);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Manager_Succeeds_AtExactlyTheApprovalLimit()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(department: Department, amount: ApprovalRules.ManagerLimit);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Manager_Fails_WhenOverTheApprovalLimit()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(department: Department, amount: OverLimit);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    [Fact]
    public async Task Manager_Fails_WhenDepartmentDoesNotMatch_EvenUnderLimit()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: OtherDepartment);
        var expense = BuildExpense(department: Department, amount: UnderLimit);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    [Fact]
    public async Task Employee_Fails()
    {
        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department, amount: UnderLimit);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }
}
