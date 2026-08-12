using System.Security.Claims;
using Meridian.DataAccess.Models;
using Meridian.Expenses.Api.Authorization;
using Meridian.Services;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expenses").RequireAuthorization().WithTags("Expenses");

        // List: finance sees all, managers see their department's expenses narrowed
        // to genuine ManagerOf reports via the PDP (see ExpenseVisibilityFilter, so
        // this can't disagree with the detail endpoint's OwnerOrPrivilegedHandler),
        // everyone else sees their own.
        group.MapGet("/", async (ClaimsPrincipal user, ExpenseVisibilityFilter visibility, CancellationToken ct) =>
            Results.Ok(await visibility.GetVisibleExpensesAsync(user, ct)))
            .WithSummary("List visible expenses")
            .WithDescription("Finance sees every expense. Managers see their department's expenses, " +
                "narrowed to the employees they directly manage. Everyone else sees only their own expenses.");

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
        })
            .WithSummary("Get an expense by id")
            .WithDescription("Returns a single expense if the caller is its owner, Finance, or a manager " +
                "of the owner (for a non-Draft expense) — decided by the PDP.");

        // Create a draft for the caller. Department is derived from the caller's own
        // claim, never trusted from the request body. Amount/Category are validated
        // via CreateExpenseRequest's DataAnnotations before this handler runs.
        // Authorization: endpoint-filter fallback, delegated to the PDP — see
        // CreateExpensePdpFilter for why this doesn't use AuthorizationHandler<T,T>.
        // CreateExpensePdpFilter already rejects a caller with no department claim
        // before next() reaches this handler, so CreateAsync's null return (the
        // same "no department" case) can't happen here — the throw below is a
        // trip-wire in case that invariant is ever broken, not expected behavior.
        group.MapPost("/", async (CreateExpenseRequest request, ClaimsPrincipal user,
            IExpenseService expenses, CancellationToken ct) =>
        {
            var created = await expenses.CreateAsync(request, user.ToCallerContext(), ct)
                ?? throw new InvalidOperationException(
                    "CreateAsync returned null despite CreateExpensePdpFilter guaranteeing a department claim.");
            return Results.Created($"/expenses/{created.Id}", created);
        })
            .AddEndpointFilter<CreateExpensePdpFilter>()
            .WithSummary("Create a draft expense")
            .WithDescription("Creates a Draft expense owned by the caller. Department is derived from the " +
                "caller's own claim and is never accepted from the request body.");

        // Decide (approve or reject): resource-based check, delegated to the PDP.
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

            var result = await authz.AuthorizeAsync(user, existing, new ApprovalRequirement(request.Status));
            if (!result.Succeeded)
            {
                return Results.Forbid();
            }

            var updated = await expenses.DecideAsync(id, request.Status, user.GetUserId()!, ct);
            return updated is not null
                ? Results.Ok(updated)
                : Results.Conflict("Expense state changed; refresh and try again.");
        })
            .WithSummary("Approve or reject a submitted expense")
            .WithDescription("Transitions a Submitted expense to Approved or Rejected. Managers are limited " +
                "to expenses owned by their direct reports and to an amount limit configured in the PDP; " +
                "Finance can decide any amount.");
    }
}
