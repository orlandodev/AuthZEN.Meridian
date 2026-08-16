using Meridian.DataAccess.Models;
using System.ComponentModel.DataAnnotations;

namespace Meridian.Services.DTOs;

// The only two decision outcomes a manager/finance reviewer can apply to a
// Submitted expense today. Extend this list if a new transition is added.
// Attributes target the constructor parameter directly — see CreateExpenseRequest.
//
// RejectionReason is required when rejecting and forbidden when approving
// (Approve has nothing to say) — a plain [Required] can't express either
// direction of that, hence IValidatableObject.
public sealed record UpdateExpenseStatusRequest(
    [Required, AllowedValues(ExpenseStatus.Approved, ExpenseStatus.Rejected)]
    ExpenseStatus Status,
    [MaxLength(1000)]
    string? RejectionReason = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status == ExpenseStatus.Rejected && string.IsNullOrWhiteSpace(RejectionReason))
        {
            yield return new ValidationResult(
                "A reason is required when rejecting an expense.",
                [nameof(RejectionReason)]);
        }

        if (Status == ExpenseStatus.Approved && !string.IsNullOrWhiteSpace(RejectionReason))
        {
            yield return new ValidationResult(
                "RejectionReason must not be set when approving an expense.",
                [nameof(RejectionReason)]);
        }
    }
}
