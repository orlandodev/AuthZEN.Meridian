using System.Security.Claims;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.UnitTests.ReceiptsApi.TestSupport;

public static class AuthorizationTestData
{
    public const string OwnerUserId = "u-emma";
    public const string OtherUserId = "u-mateo";
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

    // Authorization handlers check the ReceiptDto that crosses the API boundary, not
    // the EF Core entity, so test data builds the DTO directly — same convention as
    // Expenses.Api's AuthorizationTestData.
    public static ReceiptDto BuildReceipt(string ownerUserId = OwnerUserId) =>
        new(
            Id: Guid.NewGuid(),
            ExpenseId: Guid.NewGuid(),
            OwnerUserId: ownerUserId,
            FileName: "receipt.jpg",
            ContentType: "image/jpeg",
            UploadedAt: DateTimeOffset.UtcNow);

    // UploadEligibilityHandler checks the parent Expense (fetched from
    // Expenses.Api), not a Receipt — this is the DTO that crosses that boundary.
    public static ExpenseDto BuildExpense(
        string ownerUserId = OwnerUserId, ExpenseStatus status = ExpenseStatus.Draft) =>
        new(
            Id: Guid.NewGuid(),
            OwnerUserId: ownerUserId,
            Department: Department,
            Amount: 100m,
            Currency: "USD",
            Category: "Test",
            Status: status,
            ApproverUserId: null,
            CreatedAt: DateTimeOffset.UtcNow,
            DecidedAt: null,
            RejectionReason: null);
}
