using System.ComponentModel.DataAnnotations;

namespace Meridian.Services.DTOs;

// Department is intentionally absent here: it's derived server-side from the
// caller's own claim, never trusted from the request body.
//
// Attributes target the constructor parameter directly (no `property:`
// target): the runtime's record-validation metadata now requires that for
// primary-constructor parameters and throws InvalidOperationException at
// startup/first-use if it instead finds the metadata on the synthesized
// property.
public sealed record CreateExpenseRequest(
    [Range(typeof(decimal), "0.01", "1000000")] decimal Amount,
    [Required, MaxLength(100)] string Category);
