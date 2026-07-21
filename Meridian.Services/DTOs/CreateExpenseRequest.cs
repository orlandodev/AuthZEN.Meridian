using System.ComponentModel.DataAnnotations;

namespace Meridian.Services.DTOs;

// Department is intentionally absent here: it's derived server-side from the
// caller's own claim, never trusted from the request body.
public sealed record CreateExpenseRequest(
    [property: Range(typeof(decimal), "0.01", "1000000")] decimal Amount,
    [property: Required, MaxLength(100)] string Category);
