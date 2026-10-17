package meridian.authz

import rego.v1

# Mirrors Meridian.Pdp.Core/Receipts/ReceiptRules.cs rule-for-rule.

allow if {
    input.resource.type == "receipt"
    input.action == "read"
    receipt_can_read
}

allow if {
    input.resource.type == "receipt"
    input.action == "create"
    receipt_can_create
}

# CanRead: owner always; Finance always; a Manager of the owner (no status
# gate at all here, unlike expense read).
receipt_can_read if is_owner

receipt_can_read if input.subject.role == "finance"

receipt_can_read if {
    input.subject.role == "manager"
    resource_owner_id != null
    input.facts.isManagerOf
}

# CanCreate: owner-only, and only while the parent expense (the resource
# here) is still Draft. No Finance/manager carve-out, unlike CanRead.
receipt_can_create if {
    is_owner
    resource_status != null
    lower(resource_status) == "draft"
}
