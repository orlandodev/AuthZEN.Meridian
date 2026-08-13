using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.DataAccess.Models;
using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ExpensesApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ExpensesApi.Authorization;

// Stage 3: the department/draft/manager-vs-owner scenarios this class used to
// cover now live in the PDP itself (see RulesEngineTests' Expense_Read_*
// cases). This handler's own job is narrower: build the right SARC request
// and honor whatever the PDP decides.
public class OwnerOrPrivilegedHandlerTests
{
    private static async Task<AuthorizationHandlerContext> RunAsync(
        ClaimsPrincipal user, ExpenseDto resource, IPolicyDecisionClient pdp)
    {
        var sut = new OwnerOrPrivilegedHandler(pdp);
        var context = new AuthorizationHandlerContext([new OwnerOrPrivilegedRequirement()], user, resource);
        await sut.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Succeeds_WhenPdpPermits()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department);

        var context = await RunAsync(user, expense, pdp.Object);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_WhenPdpDenies()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var expense = BuildExpense(ownerUserId: OwnerUserId);

        var context = await RunAsync(user, expense, pdp.Object);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task BuildsSarcRequest_FromCallerAndResource()
    {
        AccessEvaluationRequest? captured = null;
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(true);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, department: Department, status: ExpenseStatus.Submitted);

        await RunAsync(user, expense, pdp.Object);

        captured.Should().NotBeNull();
        captured!.Subject.Id.Should().Be(OtherUserId);
        captured.Action.Name.Should().Be("read");
        captured.Resource.Type.Should().Be("expense");
        captured.Resource.Id.Should().Be(expense.Id.ToString());
        captured.Resource.Properties.Should().NotBeNull();
        captured.Resource.Properties!["ownerId"].Should().Be(OwnerUserId);
        captured.Resource.Properties!["status"].Should().Be(ExpenseStatus.Submitted.ToString());
    }
}
