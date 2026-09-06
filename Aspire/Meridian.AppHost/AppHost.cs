// Meridian distributed application model.

var builder = DistributedApplication.CreateBuilder(args);

// --- Data plane (one Postgres container, a database per bounded context) ---
// Password is pinned via an explicit parameter (set once in user secrets) rather than
// AddPostgres's implicit generated parameter, which regenerates on every run and drifts
// out of sync with the credentials already baked into the persisted data volume.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// Client secret for the shared "meridian.pep" client (see IdentityServer's
// Config.cs) that backend APIs use to authenticate to the PDP via client
// credentials. Set once in user secrets, same as postgres-password.
var pepClientSecret = builder.AddParameter("pep-client-secret", secret: true);

// The organization's business timezone (IANA id), from appsettings.json so it
// can change without a rebuild. DepartmentSpendRules.CanExport checks its
// Monday-Friday 9am-5pm export window in this zone rather than UTC, so it
// tracks DST. Injected to the PDP (which enforces it) and the Portal (which
// displays it) from this one value so the two can't drift.
var businessTimeZone = builder.Configuration["BusinessHours:TimeZone"] ?? "America/New_York";

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
                      .WithDataVolume()
                      .WithPgAdmin();

var identityDb  = postgres.AddDatabase("identitydb");
var expensesDb  = postgres.AddDatabase("expensesdb");
var receiptsDb  = postgres.AddDatabase("receiptsdb");
var reportingDb = postgres.AddDatabase("reportingdb");
var policyDb    = postgres.AddDatabase("policydb");

// --- Blob storage (Azurite emulator) for Receipts.Api ---
var storage = builder.AddAzureStorage("storage").RunAsEmulator(azurite => azurite.WithDataVolume());
var receiptBlobs = storage.AddBlobs("blobs");

// --- Identity provider (Duende IdentityServer) ---
// Ports are pinned to match the launchSettings.json values that are already hardcoded
// into Config.cs (client redirect URIs) and Program.cs (OIDC Authority) across services —
// Aspire's default proxied endpoints pick a new random port every run otherwise.
var identity = builder.AddProject<Projects.Meridian_IdentityServer>("identityserver")
            .WithUrlForEndpoint("https", url => url.DisplayText = "Identity Server")
            .WithReference(identityDb)
            .WaitFor(identityDb);

// --- Policy Decision Point ---
var pdp = builder.AddProject<Projects.Meridian_Pdp_Service>("pdp")
                 .WithUrlForEndpoint("https", url => url.DisplayText = "Policy Decision Point")
                 .WithReference(policyDb)
                 .WithReference(identity)
                 .WithEnvironment("BusinessHours__TimeZone", businessTimeZone)
                 .WaitFor(policyDb);

// --- Enforcement points ---
// Expenses.Api, Receipts.Api, and Reporting.Api are all PEPs: they delegate
// authorization decisions to the PDP instead of enforcing in-process.
//
// Two owner-scoped, bearer-forwarded inter-service calls: Expenses.Api ->
// Receipts.Api (blocking Submit on zero receipts) and Receipts.Api ->
// Expenses.Api (looking up the parent expense's owner/status to authorize
// upload). Declare receiptsApi first so expensesApi can
// reference it inline; the reverse reference is added below once expensesApi
// exists — Aspire resource references don't need to be declared in a single
// fluent chain.
var receiptsApi = builder.AddProject<Projects.Meridian_Receipts_Api>("receipts-api")
                .WithUrlForEndpoint("https", url => url.DisplayText = "Receipts API - Scalar")
                .WithReference(receiptsDb)
                .WithReference(identity)
                .WithReference(receiptBlobs)
                .WithReference(pdp)
                .WithEnvironment("Pep__ClientSecret", pepClientSecret)
                .WaitFor(receiptsDb)
                .WaitFor(receiptBlobs)
                .WaitFor(pdp);

var expensesApi = builder.AddProject<Projects.Meridian_Expenses_Api>("expenses-api")
                .WithUrlForEndpoint("https", url => url.DisplayText = "Expenses API - Scalar")
                .WithReference(expensesDb)
                .WithReference(identity)
                .WithReference(pdp)
                .WithReference(receiptsApi)
                .WithEnvironment("Pep__ClientSecret", pepClientSecret)
                .WaitFor(expensesDb)
                .WaitFor(pdp);

receiptsApi.WithReference(expensesApi);

var reportingApi = builder.AddProject<Projects.Meridian_Reporting_Api>("reporting-api")
                .WithUrlForEndpoint("https", url => url.DisplayText = "Reporting API - Scalar")
                .WithReference(reportingDb)
                .WithReference(identity)
                .WithReference(pdp)
                .WithEnvironment("Pep__ClientSecret", pepClientSecret)
                .WaitFor(reportingDb)
                .WaitFor(pdp);

// --- User-facing portal ---
builder.AddProject<Projects.Meridian_ExpensePortal>("portal")
        .WithUrlForEndpoint("https", u => u.DisplayText = "Portal App")
        .WithReference(identity)
        .WithReference(expensesApi)
        .WithReference(receiptsApi)
        .WithReference(reportingApi)
        .WithEnvironment("BusinessHours__TimeZone", businessTimeZone)
        .WithExternalHttpEndpoints();

builder.Build().Run();
