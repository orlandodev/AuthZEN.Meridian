# Meridian — Centralized Authorization in .NET (reference implementation)

A teaching solution that starts with **traditional ASP.NET Core authorization** and
evolves into **centralized authorization** using the OpenID **AuthZEN Authorization
API 1.0** — a PDP/PEP split, observed end-to-end with **OpenTelemetry** via **.NET Aspire**.

The business domain is a corporate **expense reimbursement** system. The domain is
deliberately ordinary; the interesting part is *where the authorization decision lives*.

## Layout

```
Meridian.slnx
├── Aspire/
│   ├── Meridian.AppHost            orchestration + Postgres containers + OTEL wiring
│   └── Meridian.ServiceDefaults    shared OTEL, health, resilience, discovery, JWT auth
├── Common/
│   ├── Meridian.DataAccess         EF Core: ExpensesDbContext, IExpenseRepository
│   └── Meridian.Services           domain layer: IExpenseService, CallerContext, Roles
├── Identity/
│   └── Meridian.IdentityServer     Duende IdP: in-memory clients/scopes + test users
├── Apps/
│   └── Meridian.ExpensePortal      ASP.NET Core MVC app (OIDC) — the user-facing UI
├── Services/
│   ├── Meridian.Expenses.Api       *** Stage 0 traditional authorization lives here ***
│   ├── Meridian.Receipts.Api       skeleton PEP (fleshed out in Stage 4)
│   └── Meridian.Reporting.Api       skeleton PEP (fleshed out in Stage 4)
├── Authorization/
│   ├── Meridian.Pdp.Service        the PDP: /access/v1/evaluation, /evaluations, metadata
│   ├── AuthZen.Contracts           SARC wire model (AuthZEN 1.0)
│   └── AuthZen.Pep                 PEP client: builds requests, batches, OTEL, DI helper
└── Tests/
    ├── Meridian.AuthZen.ConformanceTests
    └── Meridian.UnitTests
```

## Run it

```
dotnet restore
dotnet run --project Aspire/Meridian.AppHost
```

Open the **Aspire dashboard** (URL printed on startup) to see every service, its logs,
traces, and metrics. The test users (password `Pass123$`) are:

| Username | Role     | Subject   |
|----------|----------|-----------|
| emma     | employee | u-emma    |
| mateo    | employee | u-mateo   |
| nadia    | manager  | u-nadia   |
| finn     | finance  | u-finn    |

## The stage roadmap (course spine)

> For the stage-by-stage annotations related to specific changes in the source, see [changelog.md](changelog.md).


- **Stage 0 — Traditional (this scaffold).** `Meridian.Expenses.Api` enforces authorization
  in-process: role policies, a resource-based ownership handler (`OwnerOrPrivilegedHandler`),
  and an imperative amount-limit rule (`ApprovalRules`). No PDP involved.
- **Stage 1 — Duplicate the ownership/role logic into `Receipts.Api`;
  show drift, N-service redeploys, and no central audit.
- **Stage 2 — Stand up the PDP.** Replace `StubPolicyEngine` with a real rules engine
  reading `policydb`.
- **Stage 3 — Convert Expenses.Api to a PEP.** Add `AuthZen.Pep`, delete the in-process
  handlers, delegate to the PDP behind the same `[Authorize]`/endpoint-filter seam.
- **Stage 4 — Reuse.** Point `Receipts.Api` and `Reporting.Api` at the same PDP.
  One policy, three enforcement points. Demo the batch `/access/v1/evaluations` from Reporting.
- **Stage 5 — Swap the PDP.** Drop in an OPA/OpenFGA-backed PDP behind the identical
  contract; PEPs don't change. Turn `ConformanceTests` into a real equivalence suite.
- **Stage 6 — Observability.** The `Meridian.AuthZen` ActivitySource + Meter (already wired
  in `ServiceDefaults` and `AuthZenPolicyDecisionClient`) light up a live permit/deny view.

## Git strategy, licensing, and third-party notices

See `GIT-AND-RELEASE-STRATEGY.md` for the tagging convention used to mark the
end of each stage and replay any of them on demand. This repo is MIT-licensed
(`LICENSE`) — but see `THIRD-PARTY-NOTICES.md` before you assume that makes
the whole stack free for production use (Duende IdentityServer, in
particular, does not follow the repo's license).

## The one idea to keep visible

Authentication (Duende — *who you are*) and authorization (the PDP — *what you can do*)
are different layers. Keep the token thin; let the PDP answer "can they?". Watching that
decision flow through one instrumented API is the whole argument for centralization.
