# Meridian.Pdp.Opa

The OPA-backed alternative to `Meridian.Pdp.Service`: the decision engine
behind the AuthZen `pdp` resource is swappable without any PEP
(`Meridian.Expenses.Api`, `Meridian.Receipts.Api`, `Meridian.Reporting.Api`)
or `AuthZen.Pep` code change.

**This project is a translator, not a policy engine.** It hosts the exact
same AuthZEN routes (`/access/v1/evaluation`, `/access/v1/evaluations`,
`/.well-known/authzen-configuration`, via `Meridian.Pdp.Core`'s shared
`AuthZenEndpoints`), but instead of deciding permit/deny itself, it gathers
facts and asks a real, separately-running **Open Policy Agent (OPA)** server
to decide.

## Where OPA actually comes from

The OPA server is the official image published by the OPA project:
[`openpolicyagent/opa`](https://hub.docker.com/r/openpolicyagent/opa) on
Docker Hub. It runs as a container resource named `"opa"` in
`Aspire/Meridian.AppHost/AppHost.cs`, pulled and run via **Podman** — the
same container runtime already used there for Postgres and the Azurite blob
emulator. The image tag is pinned explicitly in `AppHost.cs` (never
`:latest`), so a routine `podman pull` can't silently change decision
behavior out from under the conformance suite.

This project ships no OPA binary and no vendored copy of OPA — it is purely
an HTTP client of the real thing.

## Where the policy logic lives

`policies/*.rego` in this project — plain-text Rego, bind-mounted straight
into the OPA container from source (see the `WithBindMount` call in
`AppHost.cs`), never baked into an image or compiled into this host. Each
file mirrors one C# rule class in `Meridian.Pdp.Core`, rule-for-rule:

| Rego file                | Mirrors (Meridian.Pdp.Core)         |
|---------------------------|-------------------------------------|
| `authz.rego`               | The dispatch surface + default-deny, mirroring `PolicyRulesEngine`'s default-deny on an unmatched `(resource.type, action)` pair. |
| `common.rego`              | `RulePrimitives` — pure, DB-free SARC property helpers. |
| `expense.rego`             | `Expense/ExpenseRules.cs` |
| `receipt.rego`             | `Receipts/ReceiptRules.cs` |
| `department_spend.rego`    | `Reporting/DepartmentSpendRules.cs` + `Reporting/BusinessHoursPolicy.cs` |

If a decision ever looks wrong, the Rego is the place to read — it's meant
to be diffed by eye against its C# counterpart, not treated as a black box.

## How one decision flows end to end

```
PEP (Expenses/Receipts/Reporting.Api)
  -> POST /access/v1/evaluation on the "pdp" resource
  -> Meridian.Pdp.Opa (this project): AuthZenEndpoints -> OpaPolicyEngine
       - gathers subject role/department, isManagerOf, amountLimit from
         policydb via RuleWorkspace (same DB, same facts the homegrown
         PDP's RuleWorkspace reads)
       - builds an `input` document: subject, resource, action, context,
         and a `facts` block (isManagerOf, amountLimit, now, businessTimeZone)
  -> POST {opa}/v1/data/meridian/authz/allow  { "input": { ... } }
  -> real OPA server evaluates policies/*.rego against that input
  -> { "result": true | false }  (absent "result" is treated as false —
       fail-closed, mirroring the homegrown engine's default-deny)
  -> relayed back as the AccessEvaluationResponse's Decision
```

No fact that reaches Rego is trusted from the caller for anything
security-sensitive that the homegrown engine also doesn't trust from the
caller: `facts.now` comes from this host's own `TimeProvider`, never from
the PEP or the SARC request, exactly like `BusinessHoursPolicy` today.

## Running it locally

Requires Podman running (`podman machine start` if it isn't). From the
AppHost:

```bash
dotnet run --project Aspire/Meridian.AppHost
```

To inspect OPA directly, bypassing this translator entirely — useful when a
decision looks wrong and you want to know whether the bug is in the `input`
this host builds or in the Rego itself — hit the OPA container's own
endpoint (its address is in the Aspire dashboard) with the same query this
host uses:

```bash
curl -X POST http://<opa-endpoint>/v1/data/meridian/authz/allow \
  -H "Content-Type: application/json" \
  -d '{"input": {"subject": {"id": "u-emma", "role": "employee"}, "resource": {"type": "expense", "id": "1", "properties": {"ownerId": "u-emma"}}, "action": "read"}}'
```

A missing/absent `result` key means no rule matched (default-deny), not an
error.
