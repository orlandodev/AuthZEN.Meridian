using System.Security.Claims;
using Duende.IdentityServer.Test;
using Duende.IdentityModel;

namespace Meridian.IdentityServer;

// Dev users. The Subject values match Expense.OwnerUserId in the Expenses seed data.
public static class TestUsers
{
    public static List<TestUser> Users =>
    [
        Make("u-emma",  "emma",  "Emma Okafor",   "Sales",       role: "employee"),
        Make("u-mateo", "mateo", "Mateo Rossi",   "Sales",       role: "employee"),
        Make("u-nadia", "nadia", "Nadia Haddad",  "Sales",       role: "manager"),
        Make("u-finn",  "finn",  "Finn Delgado",  "Finance",     role: "finance")
    ];

    private static TestUser Make(string sub, string username, string name, string dept, string role) => new()
    {
        SubjectId = sub,
        Username = username,
        Password = Environment.GetEnvironmentVariable("TEST_USER_PASSWORD") ?? "Pass123$",
        Claims =
        [
            new Claim(JwtClaimTypes.Name, name),
            new Claim(JwtClaimTypes.Role, role),
            new Claim("department", dept),
            new Claim("employee_id", sub)
        ]
    };
}
