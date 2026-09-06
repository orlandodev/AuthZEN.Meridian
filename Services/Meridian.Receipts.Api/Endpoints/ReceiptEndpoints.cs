using System.Security.Claims;
using Meridian.DataAccess.Models;
using Meridian.Receipts.Api.Authorization;
using Meridian.Receipts.Api.Services;
using Meridian.Services;
using Meridian.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Receipts.Api.Endpoints;

public static class ReceiptEndpoints
{
    // Receipts are only ever rendered back to the browser via Content-Type-driven
    // inline streaming (no Content-Disposition), so the accepted set is restricted
    // to types a browser will never execute as script.
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "application/pdf"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".pdf"
    };

    private static bool IsAllowedReceiptFile(IFormFile file) =>
        AllowedContentTypes.Contains(file.ContentType) &&
        AllowedExtensions.Contains(Path.GetExtension(file.FileName));

    public static void MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/receipts").RequireAuthorization().WithTags("Receipts");

        // List: finance sees every receipt for the expense, managers see every receipt
        // too but narrowed to genuine ManagerOf reports via the PDP (see
        // ReceiptVisibilityFilter, so this can't disagree with the download endpoint's
        // OwnerOrPrivilegedHandler), everyone else sees only their own.
        group.MapGet("/", async (Guid expenseId, ClaimsPrincipal user,
            ReceiptVisibilityFilter visibility, CancellationToken ct) =>
            Results.Ok(await visibility.GetVisibleReceiptsAsync(expenseId, user, ct)))
            .WithSummary("List receipts for an expense")
            .WithDescription("Finance sees every receipt attached to the expense. Managers see every receipt " +
                "too, narrowed to the employees they directly manage via the PDP. Everyone else sees only " +
                "the receipts they uploaded themselves.");

        // Download: resource-based ownership check. The Stage 1 drift bug lives in
        // OwnerOrPrivilegedHandler, not here.
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user,
            IReceiptService receipts, IAuthorizationService authz, CancellationToken ct) =>
        {
            var metadata = await receipts.GetMetadataByIdAsync(id, ct);
            if (metadata is null)
            {
                return Results.NotFound();
            }

            var result = await authz.AuthorizeAsync(user, metadata, new OwnerOrPrivilegedRequirement());
            if (!result.Succeeded)
            {
                return Results.Forbid();
            }

            var download = await receipts.DownloadAsync(id, ct);
            if (download is null)
            {
                return Results.NotFound(); // metadata existed, but the blob didn't
            }

            return Results.Stream(download.Value.Content, download.Value.ContentType);
        })
            .WithSummary("Download a receipt")
            .WithDescription("Streams the receipt file back to the caller if they're its owner or Finance.");

        // Upload: metadata (expenseId) + file, multipart/form-data. Both parameters
        // need [FromForm] once more than one form-bound value is present.
        //
        // Any endpoint binding form data (including IFormFile) gets antiforgery
        // metadata attached automatically, regardless of whether antiforgery
        // middleware is registered. This API is JWT-bearer authenticated, not
        // cookie-based, so there's no CSRF exposure and no UseAntiforgery() in the
        // pipeline — without DisableAntiforgery(), every request would 500.
        group.MapPost("/", async ([FromForm] Guid expenseId, IFormFile file, ClaimsPrincipal user,
            IReceiptService receipts, ExpensesLookupClient expensesLookup, IAuthorizationService authz,
            CancellationToken ct) =>
        {
            if (!IsAllowedReceiptFile(file))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Only PNG, JPEG, or PDF files are accepted.");
            }

            // Story 4.0: owner-only upload while the expense is still Draft. Manager
            // and Finance can never upload, at any status — they're view-only on
            // receipts, full stop. Receipts.Api has no view of the expense itself, so
            // it asks Expenses.Api (see ExpensesLookupClient) rather than trusting
            // anything caller-supplied. The decision itself goes through the same
            // resource-based IAuthorizationService pattern Download uses above (see
            // UploadEligibilityHandler), rather than an inline comparison here.
            var expense = await expensesLookup.GetExpenseAsync(expenseId, ct);
            if (expense is null)
            {
                return Results.Forbid();
            }

            var result = await authz.AuthorizeAsync(user, expense, new UploadEligibilityRequirement());
            if (!result.Succeeded)
            {
                return Results.Forbid();
            }

            await using var stream = file.OpenReadStream();
            var created = await receipts.UploadAsync(
                expenseId, stream, file.FileName, file.ContentType, user.ToCallerContext(), ct);
            return Results.Created($"/receipts/{created.Id}", created);
        })
            .DisableAntiforgery()
            .WithSummary("Upload a receipt")
            .WithDescription("Attaches a PNG, JPEG, or PDF file to an expense. Only the expense's owner may " +
                "upload, and only while it's still a Draft.");
    }
}
