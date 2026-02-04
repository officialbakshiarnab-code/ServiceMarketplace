using System.Net;

namespace ServiceMarketplace.UI.Shared.Errors;

public static class ErrorMessageFormatter
{
    public static string ToFriendlyMessage(Exception exception, string fallback = "Something went wrong. Please try again.")
    {
        if (exception is null)
            return fallback;

        // Handle rate limiting errors (429 Too Many Requests)
        if (exception is HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "You've made too many requests. Please wait a moment and try again.";
        }

        var message = exception.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return fallback;

        // Check for rate limit messages in exception text
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("429", StringComparison.OrdinalIgnoreCase))
        {
            return "You've made too many requests. Please wait a moment and try again.";
        }

        if (message.Contains("System.", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("stack", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("trace", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        return message;
    }
}
