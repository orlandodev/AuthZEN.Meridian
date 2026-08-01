namespace Meridian.Reporting.Api.Authorization;

public static class Policies
{
    public const string CanViewDepartmentSpend = "CanViewDepartmentSpend";     // finance or manager
    public const string CanViewAllDepartmentSpend = "CanViewAllDepartmentSpend"; // finance only
    public const string CanExportDepartmentSpend = "CanExportDepartmentSpend"; // finance only
}
