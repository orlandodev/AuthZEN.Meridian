using System.Net;

namespace Meridian.IntegrationTests.TestSupport;

// Answers every request with the same status code and no body — stands in
// for a PDP that is down, or that rejects the PEP's token.
public sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(statusCode));
}
