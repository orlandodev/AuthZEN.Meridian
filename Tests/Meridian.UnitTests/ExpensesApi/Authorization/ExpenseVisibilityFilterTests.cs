using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.DataAccess.Models;
using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;
using static Meridian.UnitTests.ExpensesApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ExpensesApi.Authorization;

// ExpenseVisibilityFilter exists to close the list-vs-detail authorization
// drift: IExpenseService.GetVisibleExpensesAsync's manager branch fetches
// candidates by department alone, but OwnerOrPrivilegedHandler (the detail
// endpoint) authorizes each one via the PDP's ManagerOf-based CanRead. This
// filter narrows the department candidate set through the same "read" rule
// via a boxcar /access/v1/evaluations call, so the two endpoints can't
// disagree about what a manager can see.
public class ExpenseVisibilityFilterTests
{
    private static (ExpenseVisibilityFilter Sut, Mock<IExpenseService> Expenses, Mock<IPolicyDecisionClient> Pdp) Build(
        IReadOnlyList<ExpenseDto> candidates)
    {
        var expenses = new Mock<IExpenseService>();
        expenses.Setup(e => e.GetVisibleExpensesAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var pdp = new Mock<IPolicyDecisionClient>();
        var sut = new ExpenseVisibilityFilter(expenses.Object, pdp.Object);
        return (sut, expenses, pdp);
    }

    [Fact]
    public async Task Manager_NarrowsCandidatesToWhateverThePdpPermits()
    {
        var candidates = new List<ExpenseDto>
        {
            BuildExpense(ownerUserId: "u-emma"),
            BuildExpense(ownerUserId: "u-mateo"),
            BuildExpense(ownerUserId: "u-priya")
        };
        var (sut, _, pdp) = Build(candidates);
        pdp.Setup(p => p.AreAllowedAsync(It.IsAny<AccessEvaluationsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([true, false, true]);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var result = await sut.GetVisibleExpensesAsync(user, CancellationToken.None);

        result.Should().Equal(candidates[0], candidates[2]);
    }

    [Fact]
    public async Task Finance_SkipsThePdpCall_ReturnsEveryCandidate()
    {
        var candidates = new List<ExpenseDto> { BuildExpense(ownerUserId: "u-emma"), BuildExpense(ownerUserId: "u-mateo") };
        var expenses = new Mock<IExpenseService>();
        expenses.Setup(e => e.GetVisibleExpensesAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var sut = new ExpenseVisibilityFilter(expenses.Object, pdp.Object);

        var user = BuildUser(userId: OtherUserId, role: Roles.Finance, department: Department);
        var result = await sut.GetVisibleExpensesAsync(user, CancellationToken.None);

        result.Should().BeSameAs(candidates);
    }

    [Fact]
    public async Task NonManagerEmployee_SkipsThePdpCall_ReturnsEveryCandidate()
    {
        var candidates = new List<ExpenseDto> { BuildExpense() };
        var expenses = new Mock<IExpenseService>();
        expenses.Setup(e => e.GetVisibleExpensesAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var sut = new ExpenseVisibilityFilter(expenses.Object, pdp.Object);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: Department);
        var result = await sut.GetVisibleExpensesAsync(user, CancellationToken.None);

        result.Should().BeSameAs(candidates);
    }

    [Fact]
    public async Task Manager_NoCandidates_SkipsThePdpCall()
    {
        var expenses = new Mock<IExpenseService>();
        expenses.Setup(e => e.GetVisibleExpensesAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var sut = new ExpenseVisibilityFilter(expenses.Object, pdp.Object);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var result = await sut.GetVisibleExpensesAsync(user, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Manager_BuildsOneBoxcarEntryPerCandidate_WithReadAction()
    {
        var candidates = new List<ExpenseDto>
        {
            BuildExpense(ownerUserId: "u-emma", status: ExpenseStatus.Submitted),
            BuildExpense(ownerUserId: "u-mateo", status: ExpenseStatus.Draft)
        };
        var (sut, _, pdp) = Build(candidates);
        AccessEvaluationsRequest? captured = null;
        pdp.Setup(p => p.AreAllowedAsync(It.IsAny<AccessEvaluationsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync([true, true]);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        await sut.GetVisibleExpensesAsync(user, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Subject!.Id.Should().Be(OtherUserId);
        captured.Action!.Name.Should().Be("read");
        captured.Evaluations.Should().HaveCount(2);
        captured.Evaluations[0].Resource!.Id.Should().Be(candidates[0].Id.ToString());
        captured.Evaluations[0].Resource!.Properties!["ownerId"].Should().Be("u-emma");
        captured.Evaluations[0].Resource!.Properties!["status"].Should().Be("Submitted");
        captured.Evaluations[1].Resource!.Properties!["status"].Should().Be("Draft");
    }
}
