using System.Security.Claims;
using Meridian.DataAccess.Models;
using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expenses").RequireAuthorization();

        // List: finance sees all, everyone else sees their own.
        group.MapGet("/", async (ClaimsPrincipal user, IExpenseService expenses, CancellationToken ct) =>
            Results.Ok(await expenses.GetVisibleExpensesAsync(user.ToCallerContext(), ct)));

        // Read one: resource-based ownership check.
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user,
            IExpenseService expenses, IAuthorizationService authz, CancellationToken ct) =>
        {
            var expense = await expenses.GetByIdAsync(id, ct);
            if (expense is null)
            {
                return Results.NotFound();
            }

            var result = await authz.AuthorizeAsync(user, expense, new OwnerOrPrivilegedRequirement());
            return result.Succeeded ? Results.Ok(expense) : Results.Forbid();
        });

        // Create a draft for the caller. Department is derived from the caller's own
        // claim, never trusted from the request body. Amount/Category are validated
        // via CreateExpenseRequest's DataAnnotations before this handler runs.
        group.MapPost("/", async (CreateExpenseRequest request, ClaimsPrincipal user,
            IExpenseService expenses, CancellationToken ct) =>
        {
            var created = await expenses.CreateAsync(request, user.ToCallerContext(), ct);
            return created is not null
                ? Results.Created($"/expenses/{created.Id}", created)
                : Results.BadRequest("User has no department claim.");
        });

        // Decide (approve or reject): resource-based check covers role, department,
        // and amount limit together, the same way for either outcome.
        group.MapPut("/{id:guid}/status", async (Guid id, UpdateExpenseStatusRequest request, ClaimsPrincipal user,
            IExpenseService expenses, IAuthorizationService authz, CancellationToken ct) =>
        {
            var existing = await expenses.GetByIdAsync(id, ct);
            if (existing is null)
            {
                return Results.NotFound();
            }

            if (existing.Status != ExpenseStatus.Submitted)
            {
                return Results.BadRequest("Only a Submitted expense can be decided.");
            }

            var result = await authz.AuthorizeAsync(user, existing, new ApprovalRequirement());
            if (!result.Succeeded)
            {
                return Results.Forbid();
            }

            var updated = await expenses.DecideAsync(id, request.Status, user.GetUserId()!, ct);
            return updated is not null
                ? Results.Ok(updated)
                : Results.Conflict("Expense state changed; refresh and try again.");
        });
    }
}
