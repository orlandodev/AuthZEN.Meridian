using System.Security.Claims;
using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Reporting.Api.Authorization;
using Meridian.Services;
using Meridian.UnitTests.ReportingApi.TestSupport;
using Microsoft.AspNetCore.Http;

namespace Meridian.UnitTests.ReportingApi.Authorization;

// The finance-only and business-hours checks that used to sit in the export
// handler now live in DepartmentSpendRules.CanExport, evaluated against the
// PDP's own clock. This filter's own job is narrower: build the SARC request
// and honor whatever the PDP decides — see RulesEngineTests' DepartmentSpend_Export_*
// cases for the window itself.
public class DepartmentSpendExportFilterTests
{
    private static EndpointFilterInvocationContext BuildContext(ClaimsPrincipal user) =>
        EndpointFilterInvocationContext.Create(new DefaultHttpContext { User = user });

    private static async Task<(object? Result, bool NextCalled)> RunAsync(ClaimsPrincipal user, IPolicyDecisionClient pdp)
    {
        var sut = new DepartmentSpendExportFilter(pdp);
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var result = await sut.InvokeAsync(BuildContext(user), next);
        return (result, nextCalled);
    }

    [Fact]
    public async Task CallsNext_WhenPdpPermits()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = AuthorizationTestData.BuildUser(
            userId: AuthorizationTestData.FinanceUserId, role: Roles.Finance, department: "Finance");

        var (_, nextCalled) = await RunAsync(user, pdp.Object);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ShortCircuitsWithForbid_WhenPdpDenies()
    {
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = AuthorizationTestData.BuildUser(
            userId: AuthorizationTestData.FinanceUserId, role: Roles.Finance, department: "Finance");

        var (result, nextCalled) = await RunAsync(user, pdp.Object);

        nextCalled.Should().BeFalse();
        result.Should().BeAssignableTo<IResult>();
    }

    [Fact]
    public async Task BuildsExportSarcRequest_FromCallerClaims()
    {
        AccessEvaluationRequest? captured = null;
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(true);

        var user = AuthorizationTestData.BuildUser(
            userId: AuthorizationTestData.FinanceUserId, role: Roles.Finance, department: "Finance");

        await RunAsync(user, pdp.Object);

        captured.Should().NotBeNull();
        captured!.Subject.Id.Should().Be(AuthorizationTestData.FinanceUserId);
        captured.Action.Name.Should().Be("export");
        captured.Resource.Type.Should().Be("department_spend");
    }
}
