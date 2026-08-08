using System.Text.Json;
using AuthZen.Contracts;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.UnitTests.PdpService;

// RulePrimitives is the trickiest hand-written code in the engine — reading
// Dictionary<string, object> properties/context that, once a request has
// gone through real System.Text.Json deserialization (as it does over HTTP),
// contain boxed JsonElement rather than raw string/decimal. These tests
// exercise both the "constructed in-memory" and "round-tripped through JSON"
// shapes explicitly.
public class RulePrimitivesTests
{
    private static AccessEvaluationRequest RoundTrip(AccessEvaluationRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        return JsonSerializer.Deserialize<AccessEvaluationRequest>(json)!;
    }

    [Fact]
    public void GetOwnerId_PlainString_ReturnsValue()
    {
        var request = RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Submitted");

        RulePrimitives.GetOwnerId(request).Should().Be("u-emma");
    }

    [Fact]
    public void GetOwnerId_JsonElementString_ReturnsValue()
    {
        var request = RoundTrip(RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Submitted"));
        request.Resource.Properties!["ownerId"].Should().BeOfType<JsonElement>();

        RulePrimitives.GetOwnerId(request).Should().Be("u-emma");
    }

    [Fact]
    public void GetOwnerId_MissingKey_ReturnsNull()
    {
        var request = new AccessEvaluationRequest
        {
            Subject = new Subject { Type = "user", Id = "u-emma" },
            Action = new AuthZenAction { Name = "read" },
            Resource = new Resource { Type = "expense", Id = "1" }
        };

        RulePrimitives.GetOwnerId(request).Should().BeNull();
    }

    [Fact]
    public void IsOwner_MatchingSubjectAndOwnerId_ReturnsTrue()
    {
        var request = RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Submitted");

        RulePrimitives.IsOwner(request).Should().BeTrue();
    }

    [Fact]
    public void IsOwner_DifferentSubjectAndOwnerId_ReturnsFalse()
    {
        var request = RequestFactory.ExpenseRequest("u-mateo", "read", ownerId: "u-emma", status: "Submitted");

        RulePrimitives.IsOwner(request).Should().BeFalse();
    }

    [Fact]
    public void TryGetContextAmount_DecimalValue_ReturnsTrue()
    {
        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 4000m);

        RulePrimitives.TryGetContextAmount(request, out var amount).Should().BeTrue();
        amount.Should().Be(4000m);
    }

    [Fact]
    public void TryGetContextAmount_JsonElementNumber_ReturnsTrue()
    {
        var request = RoundTrip(RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 4000m));
        request.Context!["amount"].Should().BeOfType<JsonElement>();

        RulePrimitives.TryGetContextAmount(request, out var amount).Should().BeTrue();
        amount.Should().Be(4000m);
    }

    [Fact]
    public void TryGetContextAmount_MissingContext_ReturnsFalse()
    {
        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: null);

        RulePrimitives.TryGetContextAmount(request, out var amount).Should().BeFalse();
        amount.Should().Be(0m);
    }

    [Fact]
    public void TryGetContextAmount_NonNumericJsonElement_ReturnsFalse()
    {
        var request = RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted") with
        {
            Context = new Dictionary<string, object> { ["amount"] = "not-a-number" }
        };
        var roundTripped = RoundTrip(request);

        RulePrimitives.TryGetContextAmount(roundTripped, out var amount).Should().BeFalse();
        amount.Should().Be(0m);
    }

    [Fact]
    public void GetResourceStatus_ReturnsValue()
    {
        var request = RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Draft");

        RulePrimitives.GetResourceStatus(request).Should().Be("Draft");
    }

    [Fact]
    public void GetResourceDepartment_ReturnsValue()
    {
        var request = RequestFactory.DepartmentSpendRequest("u-nadia", "read", department: "Sales");

        RulePrimitives.GetResourceDepartment(request).Should().Be("Sales");
    }
}
