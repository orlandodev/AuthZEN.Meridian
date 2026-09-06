using System.Security.Claims;
using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.Reporting.Api.Authorization;
using Meridian.Services;
using Meridian.UnitTests.ReportingApi.TestSupport;
using Microsoft.AspNetCore.Http;

namespace Meridian.UnitTests.ReportingApi.Authorization;

// The endpoint-filter counterpart to Expenses.Api's CreateExpensePdpFilter
// tests: proves the filter builds its SARC request from the caller's own
// claims (the department-spend list has no persisted entity to build from)
// and gates purely on whatever the PDP decides — the manager-or-finance and
// own-department checks themselves now live in DepartmentSpendRules.CanRead.
public class DepartmentSpendReadFilterTests
{
    private static EndpointFilterInvocationContext BuildContext(ClaimsPrincipal user) =>
        EndpointFilterInvocationContext.Create(new DefaultHttpContext { User = user });

    private static async Task<(object? Result, bool NextCalled)> RunAsync(ClaimsPrincipal user, IPolicyDecisionClient pdp)
    {
        var sut = new DepartmentSpendReadFilter(pdp);
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
            userId: AuthorizationTestData.ManagerUserId, role: Roles.Manager, department: AuthorizationTestData.Department);

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
            userId: AuthorizationTestData.EmployeeUserId, role: Roles.Employee, department: AuthorizationTestData.Department);

        var (result, nextCalled) = await RunAsync(user, pdp.Object);

        nextCalled.Should().BeFalse();
        result.Should().BeAssignableTo<IResult>();
    }

    [Fact]
    public async Task BuildsReadSarcRequest_FromCallerClaims()
    {
        AccessEvaluationRequest? captured = null;
        var pdp = new Mock<IPolicyDecisionClient>();
        pdp.Setup(p => p.IsAllowedAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccessEvaluationRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(true);

        var user = AuthorizationTestData.BuildUser(
            userId: AuthorizationTestData.ManagerUserId, role: Roles.Manager, department: AuthorizationTestData.Department);

        await RunAsync(user, pdp.Object);

        captured.Should().NotBeNull();
        captured!.Subject.Id.Should().Be(AuthorizationTestData.ManagerUserId);
        captured.Action.Name.Should().Be("read");
        captured.Resource.Type.Should().Be("department_spend");
        captured.Resource.Id.Should().BeNull();
        captured.Resource.Properties.Should().NotBeNull();
        captured.Resource.Properties!["department"].Should().Be(AuthorizationTestData.Department);
    }
}
