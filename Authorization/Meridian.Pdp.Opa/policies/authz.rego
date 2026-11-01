package meridian.authz

import rego.v1

# The dispatch surface OpaPolicyEngine queries (POST /v1/data/meridian/authz),
# reading two rules from the result: `allow` and `policy_id`.
# Default-deny mirrors Meridian.Pdp.Core's PolicyRulesEngine: an unmatched
# (resource.type, action) pair — including malformed input — is denied with
# no further evaluation and leaves `policy_id` undefined. Every `allow if {...}`
# and `policy_id := ... if {...}` rule in expense.rego, receipt.rego, and
# department_spend.rego contributes to this same package — OPA merges rules
# declared under one package across multiple files, so this file only needs
# the default. The (resource.type, action) pairs are disjoint, so at most one
# `policy_id` definition ever matches.
default allow := false
