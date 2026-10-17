package meridian.authz

import rego.v1

# Mirrors Meridian.Pdp.Core/Expense/ExpenseRules.cs rule-for-rule.

allow if {
    input.resource.type == "expense"
    input.action == "create"
    expense_can_create
}

allow if {
    input.resource.type == "expense"
    input.action == "read"
    expense_can_read
}

allow if {
    input.resource.type == "expense"
    input.action == "submit"
    expense_can_submit
}

allow if {
    input.resource.type == "expense"
    input.action in {"approve", "reject"}
    expense_can_decide
}

# CanCreate: owner, and the resource's department matches the subject's own
# department (case-insensitive, like the C# OrdinalIgnoreCase comparison).
expense_can_create if {
    is_owner
    input.subject.department != null
    resource_department != null
    lower(resource_department) == lower(input.subject.department)
}

# CanRead: owner always; Finance always; a Manager of the owner, but never
# while the expense is still Draft (the carve-out holds even for a genuine
# manager-of relationship).
expense_can_read if is_owner

expense_can_read if input.subject.role == "finance"

expense_can_read if {
    input.subject.role == "manager"
    resource_owner_id != null
    resource_status != null
    lower(resource_status) != "draft"
    input.facts.isManagerOf
}

# CanSubmit: owner-only, full stop — no Finance/manager carve-out.
expense_can_submit if is_owner

# CanDecide (approve/reject): Finance unconditionally, any amount; a Manager
# of the owner only while Submitted and only up to the configured limit.
expense_can_decide if input.subject.role == "finance"

expense_can_decide if {
    input.subject.role == "manager"
    resource_owner_id != null
    resource_status != null
    lower(resource_status) == "submitted"
    input.facts.isManagerOf
    context_amount != null
    context_amount <= input.facts.amountLimit
}
