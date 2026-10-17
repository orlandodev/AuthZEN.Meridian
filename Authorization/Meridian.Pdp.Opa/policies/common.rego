package meridian.authz

import rego.v1

# Pure, DB-free helpers reading SARC-shaped input — the Rego mirror of
# Meridian.Pdp.Core's RulePrimitives. Resource/context values here are
# whatever the caller's SARC request already carried (never looked up);
# RuleWorkspace-derived facts (role, department, isManagerOf, amountLimit)
# arrive separately under input.subject / input.facts, gathered by
# OpaPolicyEngine before it ever calls into this policy.
is_owner if input.resource.properties.ownerId == input.subject.id

resource_owner_id := input.resource.properties.ownerId

resource_status := input.resource.properties.status

resource_department := input.resource.properties.department

context_amount := input.context.amount
