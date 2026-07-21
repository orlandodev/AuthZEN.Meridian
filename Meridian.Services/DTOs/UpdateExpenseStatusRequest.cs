using Meridian.DataAccess.Models;
using System.ComponentModel.DataAnnotations;

namespace Meridian.Services.DTOs;

// The only two decision outcomes a manager/finance reviewer can apply to a
// Submitted expense today. Extend this list if a new transition is added.
public sealed record UpdateExpenseStatusRequest(
    [property: Required, AllowedValues(ExpenseStatus.Approved, ExpenseStatus.Rejected)]
    ExpenseStatus Status);
