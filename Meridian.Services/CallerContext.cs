namespace Meridian.Services;

// Decouples the service layer from System.Security.Claims / ASP.NET Core's
// ClaimsPrincipal, so services depend on a plain data record instead.
public sealed record CallerContext(string UserId, string? Department, bool IsFinance, bool IsManager);
