using Microsoft.AspNetCore.Http;

namespace Meridian.ServiceDefaults;

// Forwards the current inbound request's own Authorization header onto an
// outgoing service-to-service call. This is only safe for endpoints where the
// caller is acting on their own resource (e.g. Receipts.Api looking up the
// owner/status of an expense the caller claims to own, or Expenses.Api
// listing receipts for an expense the caller is submitting) — it is NOT a
// general "call another API as the current user" mechanism, since it grants
// the downstream call exactly the caller's own token, nothing scoped down.
// Do not reach for this where the caller isn't the resource owner; that's the
// token-exchange scenario this project has deliberately deferred.
public sealed class BearerForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inboundAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization;
        if (inboundAuth is { Count: > 0 } values)
        {
            request.Headers.TryAddWithoutValidation("Authorization", values.ToArray());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
