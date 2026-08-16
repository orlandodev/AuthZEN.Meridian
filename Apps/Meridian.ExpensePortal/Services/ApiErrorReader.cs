using System.Text.Json;

namespace Meridian.ExpensePortal.Services;

// Shared error-body parsing for every typed API client. The business APIs return
// errors in one of three shapes: a bare JSON string (Results.BadRequest("...")),
// a ProblemDetails object (Results.Problem(...)), or a validation ProblemDetails
// with a field -> messages "errors" map (minimal API's built-in AddValidation()).
// Showing any of those raw to the user reads as a broken page — this pulls out
// just the human-readable message, falling back to the raw body only if it
// isn't recognizable JSON at all.
internal static class ApiErrorReader
{
    public static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return response.ReasonPhrase;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Validation ProblemDetails: {"errors": {"Field": ["message", ...]}}.
                // Its own "title" is just "One or more validation errors occurred." —
                // the field messages are the part worth showing.
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = string.Join(" ", errors.EnumerateObject()
                        .SelectMany(field => field.Value.EnumerateArray())
                        .Select(message => message.GetString())
                        .Where(message => !string.IsNullOrWhiteSpace(message)));
                    if (!string.IsNullOrWhiteSpace(messages))
                    {
                        return messages;
                    }
                }

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    return title.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON at all — fall through and show the raw body as-is.
        }

        return body;
    }
}
