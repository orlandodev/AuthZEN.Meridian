using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.DataAccess.Models;
using Meridian.Receipts.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ReceiptsApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ReceiptsApi.Authorization;

// Stage 4 (Story 4.1): the owner+Draft matrix this class used to cover
// now lives in the PDP itself (see RulesEngineTests' Receipt_Create_* cases).
// This handler's own job is narrower: build the right SARC request — with no
// resource id, since no Receipt exists yet at upload time — and honor
// whatever the PDP decides.
public class UploadEligibilityHandlerTests
{
    private static async Task<AuthorizationHandlerContext> RunAsync(
        ClaimsPrincipal user, ExpenseDto resource, IPolicyDecisionClient pdp)
    {
        var sut = new UploadEligibilityHandler(pdp);
        var context = new AuthorizationHandlerContext([new UploadEligibilityRequirement()], user, resource);
        await sut.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Succeeds_WhenPdpPermits()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Draft);

        var context = await RunAsync(user, expense, pdp.Object);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_WhenPdpDenies()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = BuildUser(userId: OtherUserId, role: Roles.Employee, department: Department);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Draft);

        var context = await RunAsync(user, expense, pdp.Object);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task BuildsSarcRequest_WithNoResourceId_FromExpenseOwnerAndStatus()
    {
        AccessEvaluationRequest? captured = null;
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(true);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee);
        var expense = BuildExpense(ownerUserId: OwnerUserId, status: ExpenseStatus.Draft);

        await RunAsync(user, expense, pdp.Object);

        captured.Should().NotBeNull();
        captured!.Subject.Id.Should().Be(OwnerUserId);
        captured.Action.Name.Should().Be("create");
        captured.Resource.Type.Should().Be("receipt");
        captured.Resource.Id.Should().BeNull();
        captured.Resource.Properties.Should().NotBeNull();
        captured.Resource.Properties!["ownerId"].Should().Be(OwnerUserId);
        captured.Resource.Properties!["status"].Should().Be(ExpenseStatus.Draft.ToString());
    }
}
