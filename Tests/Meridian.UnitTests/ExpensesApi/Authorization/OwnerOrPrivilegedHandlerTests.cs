using Meridian.DataAccess.Models;
using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ExpensesApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ExpensesApi.Authorization;

public class OwnerOrPrivilegedHandlerTests
{
    private readonly OwnerOrPrivilegedHandler _sut = new();

    private async Task<bool> SucceedsAsync(ClaimsPrincipal user, ExpenseDto resource)
    {
        var context = new AuthorizationHandlerContext([new OwnerOrPrivilegedRequirement()], user, resource);
        await _sut.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Owner_Succeeds_EvenWithoutAPrivilegedRole()
    {
        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var expense = BuildExpense(ownerUserId: OwnerUserId);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Finance_Succeeds_RegardlessOfOwnershipOrDepartment()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Finance, department: OtherDepartment);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Manager_Succeeds_WhenDepartmentMatchesResource()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }

    [Fact]
    public async Task Manager_Fails_WhenDepartmentDoesNotMatchResource()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: OtherDepartment);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    [Fact]
    public async Task NonOwnerEmployee_Fails()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Employee, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    [Fact]
    public async Task Manager_Fails_WhenResourceIsDraft_EvenWithMatchingDepartment()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department, status: ExpenseStatus.Draft);

        (await SucceedsAsync(user, expense)).Should().BeFalse();
    }

    [Fact]
    public async Task Manager_Succeeds_WhenResourceIsSubmitted_EvenThoughNotYetDecided()
    {
        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department, status: ExpenseStatus.Submitted);

        (await SucceedsAsync(user, expense)).Should().BeTrue();
    }
}
