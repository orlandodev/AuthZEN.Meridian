package meridian.authz

import rego.v1

# Mirrors Meridian.Pdp.Core/Reporting/DepartmentSpendRules.cs and
# BusinessHoursPolicy.cs rule-for-rule. Note CanRead's department comparison
# is case-SENSITIVE (Ordinal in the C#), unlike expense_can_create's
# case-insensitive department match in expense.rego — preserved here
# deliberately, not a typo.

allow if {
    input.resource.type == "department_spend"
    input.action == "read"
    department_spend_can_read
}

allow if {
    input.resource.type == "department_spend"
    input.action == "export"
    department_spend_can_export
}

# CanRead: Finance, any department; a Manager scoped to their own department
# only (exact match).
department_spend_can_read if input.subject.role == "finance"

department_spend_can_read if {
    input.subject.role == "manager"
    resource_department != null
    input.subject.department == resource_department
}

# CanExport: Finance-only, then the business-hours gate — role first, time
# second, exactly as DepartmentSpendRules.CanExport orders it.
department_spend_can_export if {
    input.subject.role == "finance"
    within_business_hours
}

# The export window: Monday-Friday, 09:00-17:00 (exclusive upper bound) in
# facts.businessTimeZone — computed here in Rego from facts.now (the OPA
# host's own TimeProvider, in RFC3339, never a caller-supplied instant) using
# OPA's time.clock/time.weekday builtins, which resolve IANA timezone offsets
# (including DST) the same way TimeZoneInfo.ConvertTime does in the C#.
within_business_hours if {
    ns := time.parse_rfc3339_ns(input.facts.now)
    weekday := time.weekday([ns, input.facts.businessTimeZone])
    weekday != "Saturday"
    weekday != "Sunday"

    clock := time.clock([ns, input.facts.businessTimeZone])
    minutes_since_midnight := (clock[0] * 60) + clock[1]
    minutes_since_midnight >= 540 # 09:00
    minutes_since_midnight < 1020 # 17:00
}
