using System.Security.Claims;
using Meridian.Services;

namespace Meridian.UnitTests.Services;

public class ClaimsPrincipalCallerContextExtensionsTests
{
    private const string UserId = "u-emma";
    private const string Department = "Sales";

    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void ToCallerContext_MapsUserIdAndDepartment_ForAnEmployee()
    {
        var user = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim("department", Department),
            new Claim(ClaimTypes.Role, Roles.Employee));

        var caller = user.ToCallerContext();

        caller.UserId.Should().Be(UserId);
        caller.Department.Should().Be(Department);
        caller.IsFinance.Should().BeFalse();
        caller.IsManager.Should().BeFalse();
    }

    [Fact]
    public void ToCallerContext_SetsIsManager_ForAManagerRole()
    {
        var user = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(ClaimTypes.Role, Roles.Manager));

        var caller = user.ToCallerContext();

        caller.IsManager.Should().BeTrue();
        caller.IsFinance.Should().BeFalse();
    }

    [Fact]
    public void ToCallerContext_SetsIsFinance_ForAFinanceRole()
    {
        var user = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(ClaimTypes.Role, Roles.Finance));

        var caller = user.ToCallerContext();

        caller.IsFinance.Should().BeTrue();
        caller.IsManager.Should().BeFalse();
    }

    [Fact]
    public void ToCallerContext_LeavesDepartmentNull_WhenClaimAbsent()
    {
        var user = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, UserId));

        var caller = user.ToCallerContext();

        caller.Department.Should().BeNull();
    }
}
