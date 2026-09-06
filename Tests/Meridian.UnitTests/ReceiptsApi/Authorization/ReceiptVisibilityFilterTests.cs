using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Receipts.Api.Authorization;
using Meridian.Services;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;
using static Meridian.UnitTests.ReceiptsApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ReceiptsApi.Authorization;

// ReceiptVisibilityFilter exists to close the same list-vs-detail
// authorization drift ExpenseVisibilityFilter closes for expenses:
// IReceiptService.GetForExpenseAsync's manager branch fetches every receipt
// on the expense, but OwnerOrPrivilegedHandler (the download endpoint)
// authorizes each one via the PDP's ManagerOf-based CanRead. This filter
// narrows that candidate set through the same "read" rule via a boxcar
// /access/v1/evaluations call, so the two endpoints can't disagree about
// what a manager can see.
public class ReceiptVisibilityFilterTests
{
    private static readonly Guid ExpenseId = Guid.NewGuid();

    private static (ReceiptVisibilityFilter Sut, Mock<IReceiptService> Receipts, Mock<IPolicyDecisionClient> Pdp) Build(
        IReadOnlyList<ReceiptDto> candidates)
    {
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(r => r.GetForExpenseAsync(ExpenseId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var pdp = new Mock<IPolicyDecisionClient>();
        var sut = new ReceiptVisibilityFilter(receipts.Object, pdp.Object);
        return (sut, receipts, pdp);
    }

    [Fact]
    public async Task Manager_NarrowsCandidatesToWhateverThePdpPermits()
    {
        var candidates = new List<ReceiptDto>
        {
            BuildReceipt(ownerUserId: "u-emma"),
            BuildReceipt(ownerUserId: "u-mateo"),
            BuildReceipt(ownerUserId: "u-priya")
        };
        var (sut, _, pdp) = Build(candidates);
        pdp.Setup(p => p.AreAllowedAsync(It.IsAny<AccessEvaluationsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([true, false, true]);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var result = await sut.GetVisibleReceiptsAsync(ExpenseId, user, CancellationToken.None);

        result.Should().Equal(candidates[0], candidates[2]);
    }

    [Fact]
    public async Task Finance_SkipsThePdpCall_ReturnsEveryCandidate()
    {
        var candidates = new List<ReceiptDto> { BuildReceipt(ownerUserId: "u-emma"), BuildReceipt(ownerUserId: "u-mateo") };
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(r => r.GetForExpenseAsync(ExpenseId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var sut = new ReceiptVisibilityFilter(receipts.Object, pdp.Object);

        var user = BuildUser(userId: OtherUserId, role: Roles.Finance, department: Department);
        var result = await sut.GetVisibleReceiptsAsync(ExpenseId, user, CancellationToken.None);

        result.Should().BeSameAs(candidates);
    }

    [Fact]
    public async Task NonManagerEmployee_SkipsThePdpCall_ReturnsEveryCandidate()
    {
        var candidates = new List<ReceiptDto> { BuildReceipt() };
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(r => r.GetForExpenseAsync(ExpenseId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var sut = new ReceiptVisibilityFilter(receipts.Object, pdp.Object);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: Department);
        var result = await sut.GetVisibleReceiptsAsync(ExpenseId, user, CancellationToken.None);

        result.Should().BeSameAs(candidates);
    }

    [Fact]
    public async Task Manager_NoCandidates_SkipsThePdpCall()
    {
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(r => r.GetForExpenseAsync(ExpenseId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var sut = new ReceiptVisibilityFilter(receipts.Object, pdp.Object);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        var result = await sut.GetVisibleReceiptsAsync(ExpenseId, user, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Manager_BuildsOneBoxcarEntryPerCandidate_WithReadAction()
    {
        var candidates = new List<ReceiptDto>
        {
            BuildReceipt(ownerUserId: "u-emma"),
            BuildReceipt(ownerUserId: "u-mateo")
        };
        var (sut, _, pdp) = Build(candidates);
        AccessEvaluationsRequest? captured = null;
        pdp.Setup(p => p.AreAllowedAsync(It.IsAny<AccessEvaluationsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync([true, true]);

        var user = BuildUser(userId: OtherUserId, role: Roles.Manager, department: Department);
        await sut.GetVisibleReceiptsAsync(ExpenseId, user, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Subject!.Id.Should().Be(OtherUserId);
        captured.Action!.Name.Should().Be("read");
        captured.Evaluations.Should().HaveCount(2);
        captured.Evaluations[0].Resource!.Type.Should().Be("receipt");
        captured.Evaluations[0].Resource!.Id.Should().Be(candidates[0].Id.ToString());
        captured.Evaluations[0].Resource!.Properties!["ownerId"].Should().Be("u-emma");
        captured.Evaluations[1].Resource!.Properties!["ownerId"].Should().Be("u-mateo");
    }
}
