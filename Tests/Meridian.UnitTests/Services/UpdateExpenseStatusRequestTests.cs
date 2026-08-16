using System.ComponentModel.DataAnnotations;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.UnitTests.Services;

// Exercises the same Validator.TryValidateObject path Minimal API's built-in
// validation (AddValidation(), see Expenses.Api's Program.cs) uses under the
// hood, including IValidatableObject.Validate — not just the DataAnnotations
// attributes.
public class UpdateExpenseStatusRequestTests
{
    private static List<ValidationResult> Validate(UpdateExpenseStatusRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Reject_WithNoReason_FailsValidation()
    {
        var request = new UpdateExpenseStatusRequest(ExpenseStatus.Rejected);

        var results = Validate(request);

        results.Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(UpdateExpenseStatusRequest.RejectionReason));
    }

    [Fact]
    public void Reject_WithWhitespaceReason_FailsValidation()
    {
        var request = new UpdateExpenseStatusRequest(ExpenseStatus.Rejected, "   ");

        var results = Validate(request);

        results.Should().ContainSingle();
    }

    [Fact]
    public void Reject_WithReason_PassesValidation()
    {
        var request = new UpdateExpenseStatusRequest(ExpenseStatus.Rejected, "Missing an itemized receipt.");

        Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void Approve_WithNoReason_PassesValidation()
    {
        var request = new UpdateExpenseStatusRequest(ExpenseStatus.Approved);

        Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void Approve_WithReason_FailsValidation()
    {
        var request = new UpdateExpenseStatusRequest(ExpenseStatus.Approved, "Leftover text from a rejection.");

        var results = Validate(request);

        results.Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(UpdateExpenseStatusRequest.RejectionReason));
    }

    [Fact]
    public void Approve_WithWhitespaceReason_PassesValidation()
    {
        var request = new UpdateExpenseStatusRequest(ExpenseStatus.Approved, "   ");

        Validate(request).Should().BeEmpty();
    }
}
