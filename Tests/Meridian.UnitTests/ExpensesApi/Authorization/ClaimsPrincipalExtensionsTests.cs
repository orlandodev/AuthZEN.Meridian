using System.Security.Claims;
using Meridian.Services;

namespace Meridian.UnitTests.ExpensesApi.Authorization;

public class ClaimsPrincipalExtensionsTests
{
    private const string UserId = "u-emma";
    private const string SubClaimType = "sub";
    private const string DepartmentClaimType = "department";
    private const string Department = "Sales";

    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void GetUserId_ReturnsNameIdentifierClaim_WhenPresent()
    {
        var user = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, UserId));

        user.GetUserId().Should().Be(UserId);
    }

    [Fact]
    public void GetUserId_FallsBackToSubClaim_WhenNameIdentifierAbsent()
    {
        var user = BuildPrincipal(new Claim(SubClaimType, UserId));

        user.GetUserId().Should().Be(UserId);
    }

    [Fact]
    public void GetUserId_PrefersNameIdentifier_WhenBothClaimsPresent()
    {
        var user = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(SubClaimType, "different-sub-value"));

        user.GetUserId().Should().Be(UserId);
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenNeitherClaimPresent()
    {
        var user = BuildPrincipal();

        user.GetUserId().Should().BeNull();
    }

    [Fact]
    public void GetDepartment_ReturnsDepartmentClaim_WhenPresent()
    {
        var user = BuildPrincipal(new Claim(DepartmentClaimType, Department));

        user.GetDepartment().Should().Be(Department);
    }

    [Fact]
    public void GetDepartment_ReturnsNull_WhenAbsent()
    {
        var user = BuildPrincipal();

        user.GetDepartment().Should().BeNull();
    }
}
