package meridian.authz

import rego.v1

# The dispatch surface OpaPolicyEngine queries (POST /v1/data/meridian/authz/allow).
# Default-deny mirrors Meridian.Pdp.Core's PolicyRulesEngine: an unmatched
# (resource.type, action) pair — including malformed input — is denied with
# no further evaluation. Every `allow if {...}` rule in expense.rego,
# receipt.rego, and department_spend.rego contributes to this same `allow` —
# OPA merges rules declared under one package across multiple files, so this
# file only needs the default.
default allow := false
