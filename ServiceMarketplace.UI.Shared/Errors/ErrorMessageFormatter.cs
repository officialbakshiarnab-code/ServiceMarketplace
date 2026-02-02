namespace ServiceMarketplace.UI.Shared.Errors;

public static class ErrorMessageFormatter
{
    public static string ToFriendlyMessage(Exception exception, string fallback = "Something went wrong. Please try again.")
    {
        if (exception is null)
            return fallback;

        var message = exception.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return fallback;

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
