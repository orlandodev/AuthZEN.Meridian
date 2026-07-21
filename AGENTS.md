# Meridian — AI Agent Guide

**Meridian** is a teaching solution demonstrating the evolution from traditional ASP.NET Core in-process authorization to **centralized authorization** using the **AuthZEN 1.0 API**. The project is a corporate expense reimbursement system split across identity, apps, microservices, and a policy decision point (PDP).

---

## Quick Start

### Prerequisites & Build

```bash
# Verify package versions (independent):
# - .NET 10 (global.json)
# - .NET Aspire 13.4.6+ (Directory.Packages.props)
# - Duende IdentityServer 8.0.2+ (Directory.Packages.props)
# - Docker or Podman must be running for Postgres containers

dotnet restore
dotnet run --project Aspire/Meridian.AppHost
```

- **Dashboard:** Aspire dashboard auto-opens (logs, traces, metrics, health for all services)
- **Test users:** Password `Pass123$` — see [README.md](README.md#run-it) table

### Test & Verify

```bash
dotnet test Tests/Meridian.AuthZen.ConformanceTests/
```

### ⚠️ Critical First-Time Setup

1. **Add Duende Login UI** (interactive sign-in won't work without it):
   ```bash
   cd Identity/Meridian.IdentityServer
   dotnet new isui
   cd ../..
   ```
   See [README-LOGIN-UI.md](Identity/Meridian.IdentityServer/README-LOGIN-UI.md)

2. **Verify container runtime:** Docker or Podman must be running (Aspire provisions Postgres)

3. **Pin versions before first restore:** See [README.md](README.md#what-this-scaffold-is-and-isnt) — .NET 10, Aspire, and Duende are independently versioned

---

## Architecture Overview

### 6-Stage Learning Roadmap

The core idea: **authentication** (Duende — *who you are*) ≠ **authorization** (PDP — *what you can do*).

| Stage | Focus | Key Changes |
|-------|-------|------------|
| **0** (current) | Traditional | In-process RBAC + ownership handlers; no PDP |
| **1** | Pain | Duplicate rules to Receipts.Api; show drift |
| **2** | Real PDP | Replace `StubPolicyEngine` with rules-engine reading `policydb` |
| **3** | Convert APIs | Replace in-process handlers with PEP client calls to PDP |
| **4** | Reuse | Point multiple APIs at same centralized PDP; batch `/access/v1/evaluations` |
| **5** | Swap PDP | Drop OPA/OpenFGA-backed PDP; APIs unchanged (contract-driven) |
| **6** | Observability | Enable Meridian.AuthZen ActivitySource + Meter for permit/deny telemetry |

See [README.md](README.md#the-stage-roadmap-course-spine) for the full vision.

### Layered Components

```
┌─────────────────────────────────────────┐
│ Meridian.ExpensePortal (ASP.NET MVC)     │ ← OIDC Client
│ → authenticates with Duende IdP          │
└────────────┬────────────────────────────┘
             │
    ┌────────┴────────────────────────────────────┐
    │                                              │
┌───▼──────────────────┐  ┌──────────────────────┐
│ Expenses.Api         │  │ Receipts.Api         │
│ (Stage 0: traditional)  │ (Stage 0: skeleton)  │
│ - JWT bearer         │  │ → to be filled       │
│ - Role policies      │  │ in Stage 4           │
│ - Ownership handlers │  │                      │
│ - Amt limit rules    │  └──────────────────────┘
│                      │
│ + AuthZen.Pep        │ ← Stage 3+: client
│   (unused Stage 0)   │   to centralized PDP
└──────────┬───────────┘
           │
    ┌──────▼──────────────────────┐
    │ Meridian.Pdp.Service        │
    │ POST /access/v1/evaluation  │
    │ POST /access/v1/evaluations │
    │ GET  /access/v1/metadata    │
    │ (Stage 2+: real rules)      │
    │ (Stage 0: stub)             │
    └──────────────────────────────┘
```

**Key:** Authentication happens at Duende; authorization policy logic centralizes in the PDP. The PEP client (`AuthZen.Pep`) delegates decisions to the PDP via AuthZEN 1.0 HTTP contract.

---

## Key Conventions & Patterns

### Authorization Rules (Stage 0 — In-Process)

**File:** [Services/Meridian.Expenses.Api/Authorization/AuthorizationPrimitives.cs](Services/Meridian.Expenses.Api/Authorization/AuthorizationPrimitives.cs)

- **Roles:** `employee`, `manager`, `finance`
- **Policies:** `CanApprove` (manager | finance), `CanViewAll` (finance only)
- **Resource-based ownership:** `OwnerOrPrivilegedHandler` — user, manager, or finance can access an expense
- **Imperative rules:** `ApprovalRules.CanApprove()` — managers capped at $5,000; finance unlimited

**Template for new services:** Copy `AuthorizationPrimitives.cs` structure to Receipts/Reporting; later (Stage 3+), replace handlers with PEP client calls.

### Minimal APIs with Authorization

**File:** [Services/Meridian.Expenses.Api/Endpoints/ExpenseEndpoints.cs](Services/Meridian.Expenses.Api/Endpoints/ExpenseEndpoints.cs)

- Routes grouped under `/expenses`
- Declarative: `RequireAuthorization(policyName)` on route
- Imperative: Resource checks inside handlers (e.g., ownership, amount limits)
- User identity: Extracts `ClaimTypes.NameIdentifier` or `"sub"` claim from JWT

**Pattern:** Every Minimal API endpoint in Stage 0 follows this model.

### Dependency Injection & Configuration

**File:** [Services/Meridian.Expenses.Api/Program.cs](Services/Meridian.Expenses.Api/Program.cs) (template for all services)

```csharp
builder.AddServiceDefaults();              // ← ServiceDefaults (OTEL, health, discovery)
builder.AddNpgsqlDbContext<ExpenseContext>("expensesdb"); // ← Aspire-provisioned DB
builder.AddAuthentication().AddJwtBearer(...); // ← JWT from Duende
builder.AddAuthorization(opts => { ... }); // ← Policies + custom handlers
```

**ServiceDefaults:** [Aspire/Meridian.ServiceDefaults/Extensions.cs](Aspire/Meridian.ServiceDefaults/Extensions.cs)
- Wires **OpenTelemetry** (ActivitySource: `"Meridian.AuthZen"`, Meter: `"Meridian.AuthZen"`)
- Adds health checks, resilience policies, service discovery
- Called once via `builder.AddServiceDefaults()` in every service

### User Identity Flow

1. User authenticates with **Duende IdentityServer** (OIDC)
2. Token includes `sub` claim (e.g., `u-emma`) → mapped to `ClaimTypes.NameIdentifier`
3. Handler/endpoint extracts `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`
4. Used in RBAC checks (role claims) and resource ownership checks

**Test users:** See [README.md](README.md#run-it) table. Password: `Pass123$`

### Orchestration & Databases

**File:** [Aspire/Meridian.AppHost/AppHost.cs](Aspire/Meridian.AppHost/AppHost.cs)

- Single `DistributedApplication` provisions **5 Postgres databases** (identitydb, expensesdb, receiptsdb, reportingdb, policydb)
- Aspire handles **container startup ordering** (databases before services)
- Each service references the AppHost via `.WithReference(appHost)` for service discovery
- **OTEL wiring:** Aspire dashboard collects traces, metrics, logs

---

## Common Patterns & Where to Find Them

| Pattern | File | Usage |
|---------|------|-------|
| **Role policy + handler** | [AuthorizationPrimitives.cs](Services/Meridian.Expenses.Api/Authorization/AuthorizationPrimitives.cs) | Reusable template for Receipts/Reporting in Stage 4 |
| **JWT bearer + OIDC flow** | [Program.cs (Expenses.Api)](Services/Meridian.Expenses.Api/Program.cs) | Standard for all APIs; minimal change required per service |
| **Minimal API routing** | [ExpenseEndpoints.cs](Services/Meridian.Expenses.Api/Endpoints/ExpenseEndpoints.cs) | Exemplifies `RequireAuthorization()` + resource checks |
| **ServiceDefaults wiring** | [Extensions.cs (ServiceDefaults)](Aspire/Meridian.ServiceDefaults/Extensions.cs) | Single-call pattern; includes OTEL setup |
| **PDP contract (AuthZEN 1.0)** | [EvaluationModel.cs](Authorization/AuthZen.Contracts/EvaluationModel.cs) | SARC model; used by PEP client (Stage 3+) |
| **PEP client + OTEL** | [AuthZenPolicyDecisionClient.cs](Authorization/AuthZen.Pep/AuthZenPolicyDecisionClient.cs) | Instruments decisions; wired in Stage 3+ |
| **App orchestration** | [AppHost.cs](Aspire/Meridian.AppHost/AppHost.cs) | Database provisioning, service references, ordering |

---

## Common Pitfalls & Troubleshooting

### 1. **Package Version Mismatches**
   - **Issue:** Aspire SDK version in `Meridian.AppHost.csproj` doesn't match `Aspire.Hosting.*` in `Directory.Packages.props`
   - **Fix:** Verify all versions match before first `dotnet restore`; see [README.md](README.md#what-this-scaffold-is-and-isnt)

### 2. **Interactive Sign-In Not Working**
   - **Issue:** No login UI appears
   - **Cause:** Duende login UI not scaffolded
   - **Fix:** Run `dotnet new isui` in `Identity/Meridian.IdentityServer/` directory; see [README-LOGIN-UI.md](Identity/Meridian.IdentityServer/README-LOGIN-UI.md)

### 3. **Postgres Container Won't Start**
   - **Issue:** `docker: command not found` or `podman: command not found`
   - **Cause:** Docker or Podman not running
   - **Fix:** Ensure Docker Desktop or Podman is running before `dotnet run --project Aspire/Meridian.AppHost`

### 4. **Service Discovery Not Working**
   - **Issue:** Services can't resolve each other's DNS names
   - **Cause:** Services not properly registered in AppHost via `.AddProject()`/`.WithReference()`
   - **Fix:** Check [AppHost.cs](Aspire/Meridian.AppHost/AppHost.cs) for service registration; verify each service calls `builder.AddServiceDefaults()`

### 5. **PDP Wired But Unused (Stage 0)**
   - **Issue:** `AuthZen.Pep` is available but not called; authorization still in-process
   - **Cause:** This is intentional at Stage 0
   - **Action:** In Stage 3+, replace in-process handlers with PEP client calls; see Stage roadmap above

### 6. **OpenTelemetry Traces Not Exported**
   - **Issue:** Traces only in console/memory, not exported to external collector
   - **Cause:** `OTEL_EXPORTER_OTLP_ENDPOINT` not set
   - **Fix:** Set env var `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` for external OTEL push; Aspire dashboard handles it by default

---

## Workflow for Adding Authorization to a New Service

1. **Copy the template:** `AuthorizationPrimitives.cs` + role/policy definitions
2. **Add Minimal APIs:** Pattern from `ExpenseEndpoints.cs` (decl. policies + imperative checks)
3. **Wire DI:** Follow [Program.cs](Services/Meridian.Expenses.Api/Program.cs) (JWT + policies + handlers)
4. **Register in AppHost:** Add `.AddProject()` + `.WithReference()` in [AppHost.cs](Aspire/Meridian.AppHost/AppHost.cs)
5. **Test:** Ensure Aspire dashboard shows all services healthy
6. **Stage 3+:** Replace handlers with PEP client calls via `AuthZen.Pep`

---

## For More Context

- **Full roadmap & business domain:** [README.md](README.md)
- **Duende setup:** [README-LOGIN-UI.md](Identity/Meridian.IdentityServer/README-LOGIN-UI.md)
- **AuthZEN 1.0 contract:** [EvaluationModel.cs](Authorization/AuthZen.Contracts/EvaluationModel.cs)
- **PEP client + instrumentation:** [AuthZenPolicyDecisionClient.cs](Authorization/AuthZen.Pep/AuthZenPolicyDecisionClient.cs)
