using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Receipts.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Meridian.UnitTests.ReceiptsApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ReceiptsApi.Authorization;

// Stage 4 (Story 4.1): the role/ownership matrix this class used to cover
// now lives in the PDP itself (see RulesEngineTests' Receipt_Read_* cases,
// including the manager-of branch this handler never had in-process). This
// handler's own job is narrower: build the right SARC request and honor
// whatever the PDP decides.
public class OwnerOrPrivilegedHandlerTests
{
    private static async Task<AuthorizationHandlerContext> RunAsync(
        ClaimsPrincipal user, ReceiptDto resource, IPolicyDecisionClient pdp)
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
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        var context = await RunAsync(user, receipt, pdp.Object);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_WhenPdpDenies()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = BuildUser(userId: OtherUserId, role: Roles.Employee, department: Department);
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        var context = await RunAsync(user, receipt, pdp.Object);

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
        var receipt = BuildReceipt(ownerUserId: OwnerUserId);

        await RunAsync(user, receipt, pdp.Object);

        captured.Should().NotBeNull();
        captured!.Subject.Id.Should().Be(OtherUserId);
        captured.Action.Name.Should().Be("read");
        captured.Resource.Type.Should().Be("receipt");
        captured.Resource.Id.Should().Be(receipt.Id.ToString());
        captured.Resource.Properties.Should().NotBeNull();
        captured.Resource.Properties!["ownerId"].Should().Be(OwnerUserId);
    }
}
