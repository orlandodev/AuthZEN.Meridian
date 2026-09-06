# Meridian changelog

This file collects the **development-stage annotations** that were written as
inline C# comments as the solution was built, lifted here verbatim and organized
along the stage roadmap. It is a record of *how the codebase got here*, not a
description of current behavior — for the roadmap itself see
[README.md](README.md#the-stage-roadmap-course-spine) and the 6-Stage Learning
Roadmap in [AGENTS.md](AGENTS.md); for the code changes see the `[Stage N]`
commits in git history.

Each entry quotes the original comment, points at the code it annotated (by file
and enclosing type/member — line numbers shift), and is labelled
_retrospective_ (describing work already done) or _forward-looking_ (written in
an earlier stage, predicting a later one). A comment that names several stages
appears under each.

---

## Stage 0 — Traditional scaffold

`Meridian.Expenses.Api` enforces authorization in-process; the PDP is not
involved. `Receipts.Api` and `Reporting.Api` exist only as authenticated
skeletons.

### Reporting API — authenticated, not yet a PEP
> Reporting API skeleton. Stage 0: authenticated but not yet a PEP. In Stage 4 this
> service gains AuthZen.Pep and delegates every decision to the shared PDP,
> proving one policy enforced across multiple services.

`Services/Meridian.Reporting.Api/Program.cs` — top-of-file comment · _retrospective_ + _forward-looking_ (Stage 4)

### AuthZen.Pep — present but unwired
> Not yet called anywhere in this solution: no API is a PEP until Stage 3
> wires this in for Expenses.Api (and Stage 4 for Receipts/Reporting).

`Authorization/AuthZen.Pep/PepServiceCollectionExtensions.cs` — `PepServiceCollectionExtensions.AddAuthZenPep` · _forward-looking_ (Stage 3, Stage 4)

---

## Stage 1 — Duplicated ownership/role logic; deliberate drift

The ownership/role logic is copied into `Receipts.Api` (and the visibility split
into `Reporting.Api`) rather than shared, to demonstrate drift, N-service
redeploys, and the absence of a central audit trail. The canonical artifact of
the drift: `Receipt` has no `Department` field, so `Receipts.Api`'s copy of the
ownership handler never grew a manager-of branch.

### Receipts.Api ownership handler duplicated from Expenses.Api
> Stage 1 duplicated Expenses.Api's equivalent handler in-process, which is
> what let the manager-of branch drift out of sync (Receipt has no
> Department field to key off). Stage 4 (Story 4.1) delegates the decision
> itself to the PDP's ReceiptRules.CanRead — see OwnerOrPrivilegedHandler —
> so the requirement type stays, but nothing here evaluates it anymore.

`Services/Meridian.Receipts.Api/Authorization/AuthorizationPrimitives.cs` — `OwnerOrPrivilegedRequirement` · _retrospective_ (Stage 1, Stage 4)

### Download endpoint — where the drift bug lives
> Download: resource-based ownership check. The Stage 1 drift bug lives in
> OwnerOrPrivilegedHandler, not here.

`Services/Meridian.Receipts.Api/Endpoints/ReceiptEndpoints.cs` — Download (`MapGet("/{id:guid}")`) · _retrospective_

### Reporting visibility split reimplemented, not shared
> Finance sees every department's summary; a manager sees only their own
> department's. Caller identity drives which repository query runs — the
> same shape of visibility split as IExpenseService, reimplemented
> independently here rather than shared (Stage 1: authz drift).

`Common/Meridian.Services/Contracts/IReportingService.cs` — `IReportingService.GetDepartmentSpendAsync` · _retrospective_

### Receipt model — deliberately no Department field
> Deliberately no Department field — see Receipts.Api/Authorization/OwnerOrPrivilegedHandler.cs
> for why that's the Stage 1 drift bug, not an oversight.

`Common/Meridian.DataAccess/Models/Receipt.cs` — `Receipt` · _retrospective_

### PDP receipt rule normalizes past the drift
> Replicates what Meridian.Receipts.Api's OwnerOrPrivilegedHandler used to
> check in-process, but adds the manager-of branch that handler itself
> lacked (Receipt has no Department field there — documented Stage-1 drift).
> As of Stage 4 (Story 4.1), OwnerOrPrivilegedHandler and
> UploadEligibilityHandler both delegate to this PDP rule instead of
> enforcing in-process — the manager-of branch is now a live behavior
> change, not just a dormant normalization.

`Authorization/Meridian.Pdp.Service/Receipts/ReceiptRules.cs` — `ReceiptRules` (class doc) · _retrospective_ (Stage 1, Stage 4)

### PDP receipt-rule test asserts the normalization
> Proves the PDP intentionally normalizes past Receipts.Api's
> documented Stage-1 drift (no manager branch there today).

`Tests/Meridian.UnitTests/PdpService/RulesEngineTests.cs` — `Receipt_Read_ManagerOfOwner_Allowed` · _retrospective_

---

## Stage 2 — Real PDP rules engine, backed by EF Core

`StubPolicyEngine` is replaced by a rules engine reading `policydb`.

### The pluggable decision core
> The pluggable decision core. Stage 2 replaces StubPolicyEngine with a real
> rules engine reading the policy DB; Stage 5 swaps in an OPA/OpenFGA-backed
> engine behind this same interface.

`Authorization/Meridian.Pdp.Service/Pdp/IPolicyEngine.cs` — `IPolicyEngine` · _forward-looking_ (Stage 2, Stage 5)

---

## Stage 3 — Expenses.Api becomes a PEP

`AuthZen.Pep` is wired into `Expenses.Api`; the in-process resource-based
handlers stop enforcing and delegate to the PDP behind the same
`[Authorize]` / endpoint-filter seam. Declarative role policies stay in-process.

### PEP registration
> --- PEP: this API delegates authorization decisions to the PDP instead of
> enforcing in-process (Stage 3). Authenticates to the PDP as itself via
> client credentials — see the "meridian.pep" client in IdentityServer's
> Config.cs.

`Services/Meridian.Expenses.Api/Program.cs` — `AddAuthZenPep` registration · _retrospective_

### Role policies stay in-process
> --- Authorization: declarative role policies stay in-process (unchanged by
> Stage 3 — only the resource-based handlers below now delegate to the PDP) ---

`Services/Meridian.Expenses.Api/Program.cs` — `AddAuthorizationBuilder` block · _retrospective_

### OwnerOrPrivilegedHandler delegates read
> Stage 3: delegates the ownership/read decision to the PDP instead of
> enforcing in-process. The department-vs-manager scoping and the Draft
> carve-out that used to live here now live in the PDP's ExpenseRules.CanRead,
> backed by the org chart in PolicyDbContext rather than a claims comparison.

`Services/Meridian.Expenses.Api/Authorization/OwnerOrPrivilegedHandler.cs` — `OwnerOrPrivilegedHandler` · _retrospective_

### ApprovalHandler delegates approve/reject
> Stage 3: delegates the approve/reject decision to the PDP instead of
> enforcing in-process. The amount limit that used to live here as
> ApprovalRules.ManagerLimit now lives in the PDP's policy database
> (PolicyConstants.AmountLimitKeys.ExpenseApproveManagerLimit).

`Services/Meridian.Expenses.Api/Authorization/ApprovalHandler.cs` — `ApprovalHandler` · _retrospective_

### SubmitHandler delegates the owner-check
> Stage 3/Story 4.0: delegates the Submit owner-check to the PDP, the same way
> OwnerOrPrivilegedHandler and ApprovalHandler delegate read/decide — keeps
> every expense-lifecycle authorization decision in one place (ExpenseRules)
> instead of splitting it between the PDP and inline endpoint checks.

`Services/Meridian.Expenses.Api/Authorization/SubmitHandler.cs` — `SubmitHandler` · _retrospective_ (Stage 3, Stage 4 / Story 4.0)

### OwnerOrPrivilegedHandler unit tests narrowed
> Stage 3: the department/draft/manager-vs-owner scenarios this class used to
> cover now live in the PDP itself (see RulesEngineTests' Expense_Read_*
> cases). This handler's own job is narrower: build the right SARC request
> and honor whatever the PDP decides.

`Tests/Meridian.UnitTests/ExpensesApi/Authorization/OwnerOrPrivilegedHandlerTests.cs` — `OwnerOrPrivilegedHandlerTests` · _retrospective_

### ApprovalHandler unit tests narrowed
> Stage 3: the role/amount/department scenarios this class used to cover now
> live in the PDP itself (see RulesEngineTests' Expense_Decide_* cases). This
> handler's own job is narrower: map the desired outcome to the right PDP
> action, build the SARC request, and honor whatever the PDP decides.

`Tests/Meridian.UnitTests/ExpensesApi/Authorization/ApprovalHandlerTests.cs` — `ApprovalHandlerTests` · _retrospective_

### CreateExpensePdpFilter tests (Story 3.3)
> Story 3.3's endpoint-filter counterpart to the OwnerOrPrivilegedHandler/
> ApprovalHandler tests: proves the filter builds its SARC request from the
> caller's own claims (there's no persisted entity for Create to build from)
> and correctly gates on whatever the PDP decides.

`Tests/Meridian.UnitTests/ExpensesApi/Authorization/CreateExpensePdpFilterTests.cs` — `CreateExpensePdpFilterTests` · _retrospective_

### End-to-end integration proof
> End-to-end proof that Expenses.Api's Stage 3 conversion works over the
> wire: real handlers/filter -> AuthZenPolicyDecisionClient -> HTTP ->
> Pdp.Service -> PolicyRulesEngine -> decision back. Unit tests already cover
> each half in isolation with mocks; this is the seam those can't verify.

`Tests/Meridian.IntegrationTests/ExpensesApiPdpIntegrationTests.cs` — `ExpensesApiPdpIntegrationTests` · _retrospective_

### The PEP seam
> The seam every enforcement point calls. In Stage 3 the APIs stop enforcing
> in-process and start calling this instead. Swapping the PDP implementation
> (homegrown -> OPA -> OpenFGA) never changes this interface.

`Authorization/AuthZen.Pep/IPolicyDecisionClient.cs` — `IPolicyDecisionClient` · _forward-looking_

### AppHost — enforcement points overview
> Expenses.Api, Receipts.Api, and Reporting.Api are all PEPs: they delegate
> authorization decisions to the PDP instead of enforcing in-process.

`Aspire/Meridian.AppHost/AppHost.cs` — enforcement-points comment · _retrospective_ (Story 4.2 landed the last of the three)

---

## Stage 4 — Reuse: Receipts / Reporting on the same PDP (Stories 4.0–4.2)

One policy, three enforcement points. Story 4.0 adds the receipt-gating
inter-service calls and reject-with-reason; Story 4.1 converts `Receipts.Api`
to a PEP; Story 4.2 (pending) converts `Reporting.Api`.

### Receipts.Api PEP registration (Story 4.1)
> --- PEP: Stage 4 (Story 4.1) — this API delegates authorization decisions
> to the PDP instead of enforcing in-process, same as Expenses.Api since
> Stage 3. Authenticates to the PDP as itself via client credentials — see
> the shared "meridian.pep" client in IdentityServer's Config.cs.

`Services/Meridian.Receipts.Api/Program.cs` — `AddAuthZenPep` registration · _retrospective_

### Receipts.Api upload lookup client wiring (Story 4.0)
> Story 4.0: looks up the parent expense's owner/status to authorize upload —
> see ExpensesLookupClient and BearerForwardingHandler.

`Services/Meridian.Receipts.Api/Program.cs` — `AddHttpClient<ExpensesLookupClient>` · _retrospective_

### Upload-eligibility requirement (Stories 4.0 / 4.1)
> ---- Story 4.0/4.1: upload eligibility, checked against the parent Expense ----
> Deliberately narrower than OwnerOrPrivilegedRequirement: only the literal
> owner may ever upload, and only while the expense is still Draft — Finance
> and managers are never eligible, at any status. Story 4.0 evaluated this
> in-process; Story 4.1 delegates it to the PDP's ReceiptRules.CanCreate
> instead — see UploadEligibilityHandler.

`Services/Meridian.Receipts.Api/Authorization/AuthorizationPrimitives.cs` — `UploadEligibilityRequirement` · _retrospective_

### UploadEligibilityHandler delegates to the PDP (Story 4.1)
> Stage 4 (Story 4.1): delegates Story 4.0's owner+Draft upload check to the
> PDP as ("receipt", "create") instead of enforcing in-process.

`Services/Meridian.Receipts.Api/Authorization/UploadEligibilityHandler.cs` — `UploadEligibilityHandler` · _retrospective_

### Receipts.Api OwnerOrPrivilegedHandler delegates read (Story 4.1)
> Stage 4 (Story 4.1): delegates the ownership/read decision to the PDP
> instead of enforcing in-process. The manager-of branch this API's own check
> never had (see AuthorizationPrimitives.cs — Receipt has no Department field
> to key off) now applies here too, via ReceiptRules.CanRead: the Stage 1
> drift is gone.

`Services/Meridian.Receipts.Api/Authorization/OwnerOrPrivilegedHandler.cs` — `OwnerOrPrivilegedHandler` · _retrospective_ (Stage 4, Stage 1 drift)

### PDP receipt create-rule mirrors the handler (Stories 4.0 / 4.1)
> Story 4.1: mirrors Receipts.Api's own UploadEligibilityHandler exactly —
> owner-only, and only while the parent expense (the resource here; no
> Receipt exists yet at upload time) is still Draft. No Finance/manager
> carve-out, unlike CanRead above: Story 4.0 deliberately made upload
> narrower than view, and this rule preserves that instead of reusing
> CanRead's broader owner-or-privileged shape.

`Authorization/Meridian.Pdp.Service/Receipts/ReceiptRules.cs` — `ReceiptRules.CanCreate` · _retrospective_

### Upload endpoint gating (Story 4.0)
> Story 4.0: owner-only upload while the expense is still Draft. Manager
> and Finance can never upload, at any status — they're view-only on
> receipts, full stop. Receipts.Api has no view of the expense itself, so
> it asks Expenses.Api (see ExpensesLookupClient) rather than trusting
> anything caller-supplied.

`Services/Meridian.Receipts.Api/Endpoints/ReceiptEndpoints.cs` — Upload (`MapPost("/")`) · _retrospective_

### Receipts.Api → Expenses.Api lookup client (Story 4.0)
> Story 4.0: Receipts.Api's first outbound call to another Meridian API — it has no
> view of an expense's owner or status otherwise.

`Services/Meridian.Receipts.Api/Services/ExpensesLookupClient.cs` — `ExpensesLookupClient` · _retrospective_

### Expenses.Api → Receipts.Api lookup client (Story 4.0)
> Story 4.0: Expenses.Api's first outbound call to another Meridian API — blocks
> Submit when the expense has no receipts.

`Services/Meridian.Expenses.Api/Services/ReceiptsLookupClient.cs` — `ReceiptsLookupClient` · _retrospective_

### Submit receipts-gate wiring (Story 4.0)
> Story 4.0: blocks Submit when the expense has no receipts yet — see
> ReceiptsLookupClient and BearerForwardingHandler.

`Services/Meridian.Expenses.Api/Program.cs` — `AddHttpClient<ReceiptsLookupClient>` · _retrospective_

### Submit endpoint — first API-to-API call (Story 4.0)
> Submit: Draft -> Submitted, owner-only. Story 4.0's first real API-to-API
> call — blocks the transition if the expense has no receipts yet, by asking
> Receipts.Api (see ReceiptsLookupClient).

`Services/Meridian.Expenses.Api/Endpoints/ExpenseEndpoints.cs` — Submit (`MapPost("/{id:guid}/submit")`) · _retrospective_

### ExpensePortal upload-form gating (Story 4.0)
> Story 4.0: the view needs the expense's owner/status to decide whether to
> show the upload form at all — Receipts.Api itself is the real enforcement
> (see ReceiptEndpoints.MapPost in Receipts.Api), this is UX only.

`Apps/Meridian.ExpensePortal/Controllers/ReceiptsController.cs` — `ReceiptsController.ForExpense` · _retrospective_

### Receipts test data crosses the expense boundary (Story 4.0)
> Story 4.0: UploadEligibilityHandler checks the parent Expense (fetched from
> Expenses.Api), not a Receipt — this is the DTO that crosses that boundary.

`Tests/Meridian.UnitTests/ReceiptsApi/TestSupport/AuthorizationTestData.cs` — `AuthorizationTestData.BuildExpense` · _retrospective_

### PDP create-rule test — no Finance carve-out (Story 4.0)
> Unlike Receipt_Read, Finance gets no carve-out — only the literal
> owner may ever upload, per Story 4.0's fix.

`Tests/Meridian.UnitTests/PdpService/RulesEngineTests.cs` — `Receipt_Create_Finance_Denied` · _retrospective_

### Receipts.Api OwnerOrPrivilegedHandler tests narrowed (Story 4.1)
> Stage 4 (Story 4.1): the role/ownership matrix this class used to cover
> now lives in the PDP itself (see RulesEngineTests' Receipt_Read_* cases,
> including the manager-of branch this handler never had in-process). This
> handler's own job is narrower: build the right SARC request and honor
> whatever the PDP decides.

`Tests/Meridian.UnitTests/ReceiptsApi/Authorization/OwnerOrPrivilegedHandlerTests.cs` — `OwnerOrPrivilegedHandlerTests` · _retrospective_

### Receipts.Api UploadEligibilityHandler tests narrowed (Story 4.1)
> Stage 4 (Story 4.1): the owner+Draft matrix this class used to cover
> now lives in the PDP itself (see RulesEngineTests' Receipt_Create_* cases).
> This handler's own job is narrower: build the right SARC request — with no
> resource id, since no Receipt exists yet at upload time — and honor
> whatever the PDP decides.

`Tests/Meridian.UnitTests/ReceiptsApi/Authorization/UploadEligibilityHandlerTests.cs` — `UploadEligibilityHandlerTests` · _retrospective_

### Receipts.Api end-to-end integration proof (Story 4.1)
> End-to-end proof that Receipts.Api's Stage 4 (Story 4.1) conversion works
> over the wire, for both PDP-backed decisions this story converts: the real
> OwnerOrPrivilegedHandler/UploadEligibilityHandler -> AuthZenPolicyDecisionClient
> -> HTTP -> Pdp.Service -> PolicyRulesEngine -> decision back. Unit tests
> already cover each half in isolation with mocks; this is the seam those
> can't verify — including the exact regression the plan calls out: a
> manager who used to get 403 downloading a report's receipt (Receipts.Api's
> own OwnerOrPrivilegedHandler never had a manager-of branch — Stage 1
> drift) now succeeds, because ReceiptRules.CanRead does have one.

`Tests/Meridian.IntegrationTests/ReceiptsApiPdpIntegrationTests.cs` — `ReceiptsApiPdpIntegrationTests` (class doc) · _retrospective_ (Stage 4, Stage 1 drift)

### The regression the story fixes
> The regression this story exists to fix: Receipts.Api's own
> in-process check had no manager-of branch (Stage 1 drift); the
> PDP's ReceiptRules.CanRead does, so this now succeeds instead of
> 403 — over real HTTP, not a mock.

`Tests/Meridian.IntegrationTests/ReceiptsApiPdpIntegrationTests.cs` — `Download_ManagerOfOwner_Returns200` · _retrospective_ (Stage 4, Stage 1 drift)

### Upload has no privileged carve-out
> Unlike Download, Upload gets no Finance/manager carve-out — only
> the literal owner may ever upload, per Story 4.0/ReceiptRules.CanCreate.

`Tests/Meridian.IntegrationTests/ReceiptsApiPdpIntegrationTests.cs` — `Upload_NonOwnerOnDraftExpense_Returns403` · _retrospective_

### Reporting export — business-hours check is a pre-PDP context check
> Finance-only export, additionally gated by a business-hours check — the
> traditional-code version of what becomes a PDP context check in Stage 4.

`Services/Meridian.Reporting.Api/Endpoints/ReportingEndpoints.cs` — export endpoint (`MapGet("/department-spend/export")`) · _forward-looking_

### BusinessHoursPolicy becomes a PDP context check
> In Stage 4 this becomes a PDP context
> check (time-of-day/request context evaluated centrally) instead of ad hoc code
> duplicated per service.

`Services/Meridian.Reporting.Api/Authorization/BusinessHoursPolicy.cs` — `BusinessHoursPolicy` · _forward-looking_

### Reporting API gains a PEP
> Reporting API skeleton. Stage 0: authenticated but not yet a PEP. In Stage 4 this
> service gains AuthZen.Pep and delegates every decision to the shared PDP,
> proving one policy enforced across multiple services.

`Services/Meridian.Reporting.Api/Program.cs` — top-of-file comment · _forward-looking_ (also listed under Stage 0)

### Reporting.Api PEP registration (Story 4.2)
> --- PEP: this API delegates authorization decisions to the PDP instead of
> enforcing in-process, same as Expenses.Api and Receipts.Api. Authenticates
> to the PDP as itself via client credentials — see the shared "meridian.pep"
> client in IdentityServer's Config.cs.

`Services/Meridian.Reporting.Api/Program.cs` — `AddAuthZenPep` registration · _retrospective_

### Department-spend read delegated to the PDP (Story 4.2)
> The IEndpointFilter counterpart to Expenses.Api's CreateExpensePdpFilter: the
> department-spend list has no persisted resource to run through
> AuthorizationHandler<TRequirement, TResource>, so this builds the SARC
> request from the caller's own claims. Replaces the CanViewDepartmentSpend
> role policy — the manager-or-finance check, and the manager's own-department
> scoping, are now DepartmentSpendRules.CanRead. Per-department row filtering
> still happens in ReportingService.

`Services/Meridian.Reporting.Api/Authorization/DepartmentSpendReadFilter.cs` — `DepartmentSpendReadFilter` · _retrospective_

### Business-hours export check becomes a PDP evaluation (Story 4.2)
> Replaces both the CanExportDepartmentSpend role policy and the in-process
> BusinessHoursPolicy branch that used to sit inside the export handler: a
> single ("department_spend", "export") evaluation now covers finance-only
> access and the Monday-Friday 9am-5pm window together
> (DepartmentSpendRules.CanExport). This is the story's payoff — an ABAC rule
> that was a C# `if` in Stage 1 is now data the PDP reasons about. The time is
> the PDP's own, never carried in the request, so a PEP cannot widen the
> window by lying about the clock (see the PDP's own BusinessHoursPolicy
> comment); a denial collapses to one 403 regardless of which half failed.
> (Story 4.2 landed this window in UTC; the follow-up entry below moves it
> into the organization's configured business timezone.)

`Services/Meridian.Reporting.Api/Authorization/DepartmentSpendExportFilter.cs` — `DepartmentSpendExportFilter` · _retrospective_ (Stage 1, Stage 4 / Story 4.2)

### Reporting.Api in-process BusinessHoursPolicy and role policies removed (Story 4.2)
> `Reporting.Api/Authorization/BusinessHoursPolicy.cs` and its `Policies`
> constants (`CanViewDepartmentSpend`, `CanViewAllDepartmentSpend`,
> `CanExportDepartmentSpend`) are deleted — every check they backed is now the
> PDP's. This is what the forward-looking Stage 1 entries ("Reporting export —
> business-hours check is a pre-PDP context check", "BusinessHoursPolicy
> becomes a PDP context check") anticipated, minus the "carried in context"
> detail the PDP deliberately rejects.

`Services/Meridian.Reporting.Api/Authorization/BusinessHoursPolicy.cs` (deleted) · _retrospective_ (Stage 1, Story 4.2)

### Reporting.Api end-to-end integration proof (Story 4.2)
> End-to-end proof that Reporting.Api's PEP conversion works over the wire, for
> both PDP-backed decisions it converts: the real DepartmentSpendReadFilter/
> DepartmentSpendExportFilter -> AuthZenPolicyDecisionClient -> HTTP ->
> Pdp.Service -> PolicyRulesEngine -> decision back. The payoff case: the
> export business-hours window, once a C# `if` inside the endpoint, is now
> DepartmentSpendRules.CanExport decided against the PDP's own clock, pinned by
> the fixture to a weekday inside the window and a Saturday outside it.

`Tests/Meridian.IntegrationTests/ReportingApiPdpIntegrationTests.cs` — `ReportingApiPdpIntegrationTests` (class doc) · _retrospective_

### Reporting.Api filter unit tests replace BusinessHoursPolicyTests (Story 4.2)
> `BusinessHoursPolicyTests` is deleted with the class it covered; the Mon-Fri
> 9am-5pm window now lives in the PDP's own RulesEngineTests
> (DepartmentSpend_Export_* cases). The new DepartmentSpendReadFilterTests /
> DepartmentSpendExportFilterTests are narrower, like the Story 4.1 handler
> tests: build the right SARC request and honor whatever the PDP decides.

### Export window is evaluated in a configured business timezone, not UTC (feature/reporting-timezone-config)
> `DepartmentSpendRules.CanExport`'s Monday-Friday 9am-5pm gate is checked in
> the organization's business timezone rather than in UTC, so it tracks DST
> automatically. The zone id is required config with no fallback:
> `BusinessHours:TimeZone` lives in the AppHost's `appsettings.json`
> (`America/New_York`), which injects it to the PDP and the Portal; the PDP
> throws at startup if it is missing, and `DepartmentSpendRules.CanExport`
> throws rather than proceed without a zone. Still the PDP's own clock: only
> the *zone* is configuration, never a caller-supplied attribute, so the
> trusted-clock guarantee is unchanged. `BusinessHoursPolicy.IsWithinBusinessHours`
> takes a `TimeZoneInfo`; `RuleWorkspace` carries it; `PolicyRulesEngine`
> threads it through an optional ctor param (mirroring its `TimeProvider?`
> one — null only in tests that don't touch export). New RulesEngineTests
> cases prove the zone matters (inside 9-5 UTC but before 9am Eastern →
> denied), that the window follows the zone's DST offset, and that a missing
> zone throws.

`Authorization/Meridian.Pdp.Service/Reporting/BusinessHoursPolicy.cs` — `BusinessHoursPolicy.IsWithinBusinessHours` · _retrospective_

### Portal names the export window instead of describing enforcement (feature/reporting-timezone-config)
> The Reports page hint dropped its "enforced by the PDP in Stage 4, and
> imperatively in Stage 1" phrasing — internal narrative that shouldn't face
> users — for "CSV export is available Monday–Friday, 9:00 AM–5:00 PM Eastern
> Time." The timezone label is derived from the same `BusinessHours:TimeZone`
> the PDP enforces with (injected to the Portal by the AppHost), so the shown
> hours can't drift from the enforced ones. A Razor comment points at
> `DepartmentSpendExportFilter` for the enforcement path.

`Apps/Meridian.ExpensePortal/Controllers/ReportsController.cs` — `ReportsController.BuildExportWindowText` · _retrospective_

`Tests/Meridian.UnitTests/ReportingApi/Authorization/` — `DepartmentSpendReadFilterTests`, `DepartmentSpendExportFilterTests` · _retrospective_

---

## Stage 5 — Swap the PDP (OPA / OpenFGA); conformance suite

Drop in an OPA/OpenFGA-backed PDP behind the identical contract; the PEPs do not
change. Turn the conformance tests into a real equivalence suite.

### Conformance suite placeholder
> Placeholder. In Stage 5 this becomes a real conformance suite that runs the
> same SARC requests against the homegrown PDP and an OPA-backed PDP and
> asserts identical decisions — proving the standard makes PDPs interchangeable.

`Tests/Meridian.AuthZen.ConformanceTests/ContractShapeTests.cs` — `ContractShapeTests` · _forward-looking_

### Decision core swap
> The pluggable decision core. Stage 2 replaces StubPolicyEngine with a real
> rules engine reading the policy DB; Stage 5 swaps in an OPA/OpenFGA-backed
> engine behind this same interface.

`Authorization/Meridian.Pdp.Service/Pdp/IPolicyEngine.cs` — `IPolicyEngine` · _forward-looking_ (also listed under Stage 2)

---

## Stage 6 — Observability

No stage-tagged inline comments. Per [README.md](README.md#the-stage-roadmap-course-spine)
and [AGENTS.md](AGENTS.md), the `Meridian.AuthZen` `ActivitySource` + `Meter`
are already wired in `ServiceDefaults` and `AuthZenPolicyDecisionClient`; this
stage lights up a live permit/deny view over them.

---

## Maintaining this file

**Stage and story narrative goes here, not into inline comments.** When a change
belongs to a stage or story:

1. Add an entry under that stage's section: a short label, the note itself, and a
   reference to the file + enclosing type/member (not a line number, which
   shifts).
2. Keep the corresponding source comments about **current behavior only** — no
   "Stage N did…", "in Stage N this becomes…", "(Stage 1 drift)", "Story 4.0:"
   prefixes.
3. When a new stage begins, add its `## Stage N — …` section in roadmap order.

Architectural notes that name a technology but **no stage** may stay inline —
e.g. `Authorization/AuthZen.Pep/IPolicyDecisionClient.cs` ("swapping the PDP
implementation never changes this interface") and
`Authorization/Meridian.Pdp.Service/Pdp/IPolicyEngine.cs` (implementations swap
in behind the interface).

See [AGENTS.md](AGENTS.md#recording-stage-progress--changelogmd-not-inline-comments)
for the same convention in the agent guide.
