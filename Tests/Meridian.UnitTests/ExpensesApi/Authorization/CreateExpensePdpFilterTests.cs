using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using static Meridian.UnitTests.ExpensesApi.TestSupport.AuthorizationTestData;

namespace Meridian.UnitTests.ExpensesApi.Authorization;

// Story 3.3's endpoint-filter counterpart to the OwnerOrPrivilegedHandler/
// ApprovalHandler tests: proves the filter builds its SARC request from the
// caller's own claims (there's no persisted entity for Create to build from)
// and correctly gates on whatever the PDP decides.
public class CreateExpensePdpFilterTests
{
    private static EndpointFilterInvocationContext BuildContext(ClaimsPrincipal user) =>
        EndpointFilterInvocationContext.Create(new DefaultHttpContext { User = user });

    private static async Task<(object? Result, bool NextCalled)> RunAsync(ClaimsPrincipal user, IPolicyDecisionClient pdp)
    {
        var sut = new CreateExpensePdpFilter(pdp);
        var context = BuildContext(user);
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var result = await sut.InvokeAsync(context, next);
        return (result, nextCalled);
    }

    [Fact]
    public async Task CallsNext_WhenPdpPermits()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: Department);

        var (_, nextCalled) = await RunAsync(user, pdp.Object);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ShortCircuits_WhenPdpDenies()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: Department);

        var (result, nextCalled) = await RunAsync(user, pdp.Object);

        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ShortCircuits_WhenCallerHasNoDepartmentClaim_WithoutCallingPdp()
    {
        var pdp = new Mock<IPolicyDecisionClient>(MockBehavior.Strict);
        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: null);

        var (result, nextCalled) = await RunAsync(user, pdp.Object);

        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildsSarcRequest_FromCallerClaims_NotAPersistedEntity()
    {
        AccessEvaluationRequest? captured = null;
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(true);

        var user = BuildUser(userId: OwnerUserId, role: Roles.Employee, department: Department);

        await RunAsync(user, pdp.Object);

        captured.Should().NotBeNull();
        captured!.Subject.Id.Should().Be(OwnerUserId);
        captured.Action.Name.Should().Be("create");
        captured.Resource.Type.Should().Be("expense");
        captured.Resource.Id.Should().BeNull();
        captured.Resource.Properties.Should().NotBeNull();
        captured.Resource.Properties!["ownerId"].Should().Be(OwnerUserId);
        captured.Resource.Properties!["department"].Should().Be(Department);
    }
}
