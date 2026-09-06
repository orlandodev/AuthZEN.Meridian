using System.Security.Claims;

namespace Meridian.UnitTests.ReportingApi.TestSupport;

public static class AuthorizationTestData
{
    public const string FinanceUserId = "u-finn";
    public const string ManagerUserId = "u-nadia";
    public const string EmployeeUserId = "u-emma";
    public const string Department = "Sales";

    public static ClaimsPrincipal BuildUser(string? userId = null, string? role = null, string? department = null)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        if (department is not null)
        {
            claims.Add(new Claim("department", department));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
