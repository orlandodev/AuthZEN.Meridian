namespace Meridian.Pdp.Core.Pdp;

// Engine-neutral policy identifiers reported in every decision's response
// context. They name a business policy, not an implementation — never a C#
// rule method or a Rego rule — so every IPolicyEngine reports the same ID for
// the same request. The Rego policies in Meridian.Pdp.Opa declare these same
// strings as `policy_id`.
public static class PolicyIds
{
    public const string ExpenseCreate = "expense.create";
    public const string ExpenseRead = "expense.read";
    public const string ExpenseSubmit = "expense.submit";
    public const string ExpenseDecide = "expense.decide";
    public const string ReceiptRead = "receipt.read";
    public const string ReceiptCreate = "receipt.create";
    public const string DepartmentSpendRead = "department_spend.read";
    public const string DepartmentSpendExport = "department_spend.export";
}
