using System.Text.Json;
using AuthZen.Contracts;
using FluentAssertions;
using Xunit;

namespace Meridian.AuthZen.ConformanceTests;

// Placeholder for the PDP conformance suite: identical SARC requests run
// against interchangeable PDP implementations must yield identical decisions,
// proving the standard makes PDPs interchangeable.
public class ContractShapeTests
{
    [Fact]
    public void EvaluationRequest_Serializes_ToSarcShape()
    {
        var request = new AccessEvaluationRequest
        {
            Subject = new Subject { Type = "user", Id = "u-emma" },
            Action = new AuthZenAction { Name = "can_approve" },
            Resource = new Resource { Type = "expense", Id = "123" }
        };

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"subject\"");
        json.Should().Contain("\"action\"");
        json.Should().Contain("\"resource\"");
    }

    [Fact]
    public void EvaluationsRequest_Serializes_ToSarcBoxcarShape()
    {
        // Mirrors the spec's boxcar example: top-level subject/action are
        // defaults, and entries only carry what they override (typically
        // just resource) — see AuthZEN 1.0 section 7.1.1.
        var batch = new AccessEvaluationsRequest
        {
            Subject = new Subject { Type = "user", Id = "u-emma" },
            Action = new AuthZenAction { Name = "can_read" },
            Evaluations =
            [
                new EvaluationEntry { Resource = new Resource { Type = "document", Id = "boxcarring.md" } },
                new EvaluationEntry { Resource = new Resource { Type = "document", Id = "subject-search.md" } }
            ]
        };

        var json = JsonSerializer.Serialize(batch);

        json.Should().Contain("\"subject\"");
        json.Should().Contain("\"action\"");
        json.Should().Contain("\"evaluations\"");
        json.Should().NotContain("\"ownerId\"");
        // Entries omit subject/action entirely — no stray nulls in the wire shape.
        var evaluationsJson = JsonSerializer.Serialize(batch.Evaluations[0]);
        evaluationsJson.Should().NotContain("\"subject\"");
        evaluationsJson.Should().NotContain("\"action\"");
    }

    [Fact]
    public void EvaluationResponse_WithoutContext_OmitsContextField()
    {
        var response = new AccessEvaluationResponse { Decision = true };

        var json = JsonSerializer.Serialize(response);

        json.Should().Contain("\"decision\"");
        json.Should().NotContain("\"context\"");
    }
}
