// Meridian distributed application model.
// Stage 0: the three APIs still enforce authorization in-process; the PDP is
// wired up and running but no API depends on it yet. As you progress through
// the stages you'll add .WithReference(pdp) to each API and the portal.

var builder = DistributedApplication.CreateBuilder(args);

// --- Data plane (one Postgres container, a database per bounded context) ---
// Password is pinned via an explicit parameter (set once in user secrets) rather than
// AddPostgres's implicit generated parameter, which regenerates on every run and drifts
// out of sync with the credentials already baked into the persisted data volume.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

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
                 .WaitFor(policyDb);

// --- Enforcement points (Stage 0: no PDP reference yet) ---
var expensesApi = builder.AddProject<Projects.Meridian_Expenses_Api>("expenses-api")
                .WithUrlForEndpoint("https", url => url.DisplayText = "Expenses API - Scalar")
                .WithReference(expensesDb)
                .WithReference(identity)
                .WaitFor(expensesDb);

var receiptsApi = builder.AddProject<Projects.Meridian_Receipts_Api>("receipts-api")
                .WithUrlForEndpoint("https", url => url.DisplayText = "Receipts API - Scalar")
                .WithReference(receiptsDb)
                .WithReference(identity)
                .WithReference(receiptBlobs)
                .WaitFor(receiptsDb)
                .WaitFor(receiptBlobs);

var reportingApi = builder.AddProject<Projects.Meridian_Reporting_Api>("reporting-api")
                .WithUrlForEndpoint("https", url => url.DisplayText = "Reporting API - Scalar")
                .WithReference(reportingDb)
                .WithReference(identity)
                .WaitFor(reportingDb);

// --- User-facing portal ---
builder.AddProject<Projects.Meridian_ExpensePortal>("portal")
        .WithUrlForEndpoint("https", u => u.DisplayText = "Portal App")
        .WithReference(identity)
        .WithReference(expensesApi)
        .WithReference(receiptsApi)
        .WithReference(reportingApi)
        .WithExternalHttpEndpoints();

builder.Build().Run();
