namespace Meridian.DataAccess.PdP;

// Role names and amount-limit config keys used by the PDP's policy data
// model (RoleAssignment.Role, AmountLimitConfig.Key). These intentionally
// duplicate Meridian.Services.Roles' string values rather than the PDP
// referencing that project directly — role names are a wire-level/data
// convention shared across services, not a shared code dependency.
public static class PolicyConstants
{
    public static class RoleNames
    {
        public const string Employee = "employee";
        public const string Manager = "manager";
        public const string Finance = "finance";
    }

    public static class AmountLimitKeys
    {
        public const string ExpenseApproveManagerLimit = "expense.approve.manager_limit";
    }
}
