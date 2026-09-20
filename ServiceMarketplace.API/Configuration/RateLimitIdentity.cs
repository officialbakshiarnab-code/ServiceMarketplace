using System.Security.Claims;

namespace ServiceMarketplace.API.Configuration;

public static class RateLimitIdentity
{
    public static string GetPartitionKey(HttpContext context)
    {
        var identity = context.User.Identities.FirstOrDefault(i => i.IsAuthenticated);
        var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
