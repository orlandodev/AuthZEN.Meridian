using Meridian.DataAccess.Models;
using System.ComponentModel.DataAnnotations;

namespace Meridian.Services.DTOs;

// The only two decision outcomes a manager/finance reviewer can apply to a
// Submitted expense today. Extend this list if a new transition is added.
// Attributes target the constructor parameter directly — see CreateExpenseRequest.
public sealed record UpdateExpenseStatusRequest(
    [Required, AllowedValues(ExpenseStatus.Approved, ExpenseStatus.Rejected)]
    ExpenseStatus Status);
