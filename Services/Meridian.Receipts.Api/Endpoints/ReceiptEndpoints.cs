using System.Security.Claims;
using Meridian.Receipts.Api.Authorization;
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
        var group = app.MapGroup("/receipts").RequireAuthorization();

        // List: finance sees every receipt for the expense, everyone else sees only
        // their own — no department filtering, matching how GET /expenses itself has
        // no department logic (that only shows up in the resource-based checks below).
        group.MapGet("/", async (Guid expenseId, ClaimsPrincipal user,
            IReceiptService receipts, CancellationToken ct) =>
            Results.Ok(await receipts.GetForExpenseAsync(expenseId, user.ToCallerContext(), ct)));

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
        });

        // Upload: metadata (expenseId) + file, multipart/form-data. Both parameters
        // need [FromForm] once more than one form-bound value is present.
        //
        // Any minimal API endpoint that binds form data — including IFormFile — gets
        // antiforgery metadata attached automatically in .NET 8+, regardless of whether
        // antiforgery middleware is registered. This API is JWT-bearer authenticated,
        // not cookie-based, so there's no CSRF exposure to protect against here, and no
        // UseAntiforgery() is registered anywhere in the pipeline — without this call the
        // endpoint's metadata demands a check that nothing can perform, and every request
        // throws InvalidOperationException before the handler ever runs.
        group.MapPost("/", async ([FromForm] Guid expenseId, IFormFile file, ClaimsPrincipal user,
            IReceiptService receipts, IAuthorizationService authz, CancellationToken ct) =>
        {
            if (!IsAllowedReceiptFile(file))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Only PNG, JPEG, or PDF files are accepted.");
            }

            // Resource-based ownership check, same OwnerOrPrivilegedRequirement pattern
            // as the download path. Receipts.Api has no view of Expense ownership (and
            // no inter-service call to Expenses.Api to get one), so the closest resource
            // it can check against is any receipt already on file for this expenseId:
            // once one exists, only its owner (or finance) may attach more. The first
            // receipt uploaded for an expense establishes that expense's owner here.
            var existing = await receipts.GetAnyMetadataForExpenseAsync(expenseId, ct);
            if (existing is not null)
            {
                var result = await authz.AuthorizeAsync(user, existing, new OwnerOrPrivilegedRequirement());
                if (!result.Succeeded)
                {
                    return Results.Forbid();
                }
            }

            await using var stream = file.OpenReadStream();
            var created = await receipts.UploadAsync(
                expenseId, stream, file.FileName, file.ContentType, user.ToCallerContext(), ct);
            return Results.Created($"/receipts/{created.Id}", created);
        }).DisableAntiforgery();
    }
}
