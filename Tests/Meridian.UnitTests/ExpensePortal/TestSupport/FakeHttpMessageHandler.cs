namespace Meridian.UnitTests.ExpensePortal.TestSupport;

// HttpMessageHandler.SendAsync is protected, so it can't be mocked directly
// with Moq without the Protected() extension; a small fake is simpler and
// gives full control over the request/response pair being asserted on.
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(responder(request));
    }

    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler(responder);
        return new HttpClient(handler) { BaseAddress = new Uri("https://expenses-api.test/") };
    }
}
