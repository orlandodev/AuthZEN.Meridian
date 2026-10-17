extern alias PdpOpaAssembly;

using System.Net;
using System.Text;
using System.Text.Json;
using Meridian.DataAccess.PdP;
using Meridian.UnitTests.PdpService;
using Meridian.UnitTests.TestSupport;
using PdpOpaAssembly::Meridian.Pdp.Opa.Pdp;

namespace Meridian.UnitTests.PdpOpa;

public class OpaPolicyEngineTests
{
    private static readonly TimeZoneInfo BusinessZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static readonly DateTimeOffset FixedNow = new(2026, 7, 23, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("""{"result":true}""", true)]
    [InlineData("""{"result":false}""", false)]
    [InlineData("""{}""", false)]
    public async Task EvaluateAsync_OpaResponse_RelaysDecisionFailingClosedOnMissingResult(string opaBody, bool expected)
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = CreateEngine(db, OpaReturns(opaBody), out _);

        var decision = await engine.EvaluateAsync(
            RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Draft"));

        decision.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateAsync_OpaReturnsServerError_Throws()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = CreateEngine(db, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);

        var act = () => engine.EvaluateAsync(
            RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Draft"));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task EvaluateAsync_AnyRequest_QueriesAllowRule()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = CreateEngine(db, OpaReturns("""{"result":true}"""), out var capture);

        await engine.EvaluateAsync(RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Draft"));

        capture.RequestUri!.AbsolutePath.Should().Be("/v1/data/meridian/authz/allow");
    }

    [Fact]
    public async Task EvaluateAsync_ManagerOfOwner_SendsPolicyDbFactsInInput()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = CreateEngine(db, OpaReturns("""{"result":true}"""), out var capture);

        await engine.EvaluateAsync(
            RequestFactory.ExpenseRequest("u-nadia", "approve", ownerId: "u-emma", status: "Submitted", contextAmount: 1200m));

        var input = capture.Input;
        input.GetProperty("subject").GetProperty("id").GetString().Should().Be("u-nadia");
        input.GetProperty("subject").GetProperty("role").GetString().Should().Be("manager");
        input.GetProperty("subject").GetProperty("department").GetString().Should().Be("Sales");
        input.GetProperty("action").GetString().Should().Be("approve");
        input.GetProperty("resource").GetProperty("type").GetString().Should().Be("expense");
        input.GetProperty("resource").GetProperty("properties").GetProperty("ownerId").GetString().Should().Be("u-emma");
        input.GetProperty("context").GetProperty("amount").GetDecimal().Should().Be(1200m);
        input.GetProperty("facts").GetProperty("isManagerOf").GetBoolean().Should().BeTrue();
        input.GetProperty("facts").GetProperty("amountLimit").GetDecimal().Should().Be(5000m);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownSubject_SendsNullRoleAndNoManagerOf()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = CreateEngine(db, OpaReturns("""{"result":false}"""), out var capture);

        await engine.EvaluateAsync(RequestFactory.ExpenseRequest("u-nobody", "read", ownerId: "u-emma", status: "Submitted"));

        capture.Input.GetProperty("subject").GetProperty("role").ValueKind.Should().Be(JsonValueKind.Null);
        capture.Input.GetProperty("facts").GetProperty("isManagerOf").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_AnyRequest_SendsHostClockNotCallerTime()
    {
        using var db = PolicyDbContextTestFactory.Create();
        var engine = CreateEngine(db, OpaReturns("""{"result":true}"""), out var capture);

        await engine.EvaluateAsync(RequestFactory.DepartmentSpendRequest("u-finn", "export", "Sales"));

        capture.Input.GetProperty("facts").GetProperty("now").GetDateTimeOffset().Should().Be(FixedNow);
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Eastern Standard Time")]
    public async Task EvaluateAsync_IanaOrWindowsZoneId_SendsIanaZoneToOpa(string configuredZoneId)
    {
        using var db = PolicyDbContextTestFactory.Create();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(configuredZoneId);
        var engine = CreateEngine(db, OpaReturns("""{"result":true}"""), out var capture, zone);

        await engine.EvaluateAsync(RequestFactory.DepartmentSpendRequest("u-finn", "export", "Sales"));

        capture.Input.GetProperty("facts").GetProperty("businessTimeZone").GetString().Should().Be("America/New_York");
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> OpaReturns(string body) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private static OpaPolicyEngine CreateEngine(
        PolicyDbContext db,
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        out OpaRequestCapture capture,
        TimeZoneInfo? zone = null)
    {
        capture = new OpaRequestCapture(responder);
        var client = new HttpClient(capture) { BaseAddress = new Uri("http://opa.test/") };
        return new OpaPolicyEngine(client, db, zone ?? BusinessZone, new FakeTimeProvider(FixedNow));
    }

    // Reads the body inside SendAsync, before the caller disposes the request.
    private sealed class OpaRequestCapture(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public JsonElement Input { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            var json = await request.Content!.ReadAsStringAsync(cancellationToken);
            Input = JsonDocument.Parse(json).RootElement.GetProperty("input").Clone();
            return responder(request);
        }
    }
}
