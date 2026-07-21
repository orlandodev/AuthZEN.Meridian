using System.Text.Json;
using AuthZen.Contracts;
using Xunit;

namespace Meridian.AuthZen.ConformanceTests;

// Placeholder. In Stage 5 this becomes a real conformance suite that runs the
// same SARC requests against the homegrown PDP and an OPA-backed PDP and
// asserts identical decisions — proving the standard makes PDPs interchangeable.
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

        Assert.Contains("\"subject\"", json);
        Assert.Contains("\"action\"", json);
        Assert.Contains("\"resource\"", json);
    }
}
