using Meridian.DataAccess;
using Meridian.DataAccess.Receipts;
using Meridian.Receipts.Api.Authorization;
using Meridian.Receipts.Api.Endpoints;
using Meridian.Receipts.Api.Services;
using Meridian.ServiceDefaults;
using Meridian.Services;
using Meridian.Services.Contracts;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddBearerForwarding();
builder.AddMeridianOpenApi(
    title: "Meridian Receipts API",
    description: "Upload, list, and download receipt files (PNG, JPEG, or PDF) attached to expenses.");

// EF Core against the Aspire-provisioned Postgres database "receiptsdb".
builder.AddNpgsqlDbContext<ReceiptsDbContext>("receiptsdb");

// Blob client against the Aspire-provisioned Azurite emulator resource "blobs".
builder.AddAzureBlobServiceClient("blobs");

// Register built-in Minimal API validation
builder.Services.AddValidation();

// --- Authentication: validate JWTs issued by the Duende IdentityServer ---
builder.AddMeridianApiAuthentication(audience: "meridian.receipts.api");

// --- Authorization (Stage 1: duplicated from Expenses.Api, deliberately incomplete) ---
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationHandler, OwnerOrPrivilegedHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, UploadEligibilityHandler>();

// Story 4.0: looks up the parent expense's owner/status to authorize upload —
// see ExpensesLookupClient and BearerForwardingHandler.
builder.Services.AddHttpClient<ExpensesLookupClient>(c => c.BaseAddress = new("https+http://expenses-api"))
    .AddHttpMessageHandler<BearerForwardingHandler>();

builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddSingleton<IReceiptBlobStorage, AzureBlobReceiptStorage>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();

var app = builder.Build();
app.UseApiExceptionHandling();
app.MapDefaultEndpoints();
app.MapMeridianOpenApi(new Dictionary<string, string>
{
    { "meridian.receipts.api", "Resource access: Meridian Receipts API" }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapReceiptEndpoints();

await app.Services.MigrateOrEnsureCreatedAsync<ReceiptsDbContext>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReceiptsDbContext>();
    var blobStorage = scope.ServiceProvider.GetRequiredService<IReceiptBlobStorage>();
    await ReceiptBlobContentSeeder.EnsureBlobContentAsync(db, blobStorage);
}

app.Run();
