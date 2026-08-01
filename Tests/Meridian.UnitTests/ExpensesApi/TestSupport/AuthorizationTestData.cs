using System.Security.Claims;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.UnitTests.ExpensesApi.TestSupport;

public static class AuthorizationTestData
{
    public const string OwnerUserId = "u-emma";
    public const string OtherUserId = "u-mateo";
    public const string Department = "Sales";
    public const string OtherDepartment = "Ops";

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

    // Authorization handlers now check the ExpenseDto that crosses the API
    // boundary, not the EF Core entity, so test data builds the DTO directly.
    public static ExpenseDto BuildExpense(
        string ownerUserId = OwnerUserId, string department = Department, decimal amount = 100m,
        ExpenseStatus status = ExpenseStatus.Submitted) =>
        new(
            Id: Guid.NewGuid(),
            OwnerUserId: ownerUserId,
            Department: department,
            Amount: amount,
            Currency: "USD",
            Category: "Test",
            Status: status,
            ApproverUserId: null,
            CreatedAt: DateTimeOffset.UtcNow,
            DecidedAt: null);
}
