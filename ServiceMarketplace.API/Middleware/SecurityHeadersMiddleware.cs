namespace ServiceMarketplace.API.Middleware;

/// <summary>
/// Middleware that adds security headers to all HTTP responses.
/// Implements defense-in-depth security practices.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        // Prevent clickjacking attacks
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        
        // Enable XSS protection in older browsers
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        
        // Control referrer information
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Restrict feature permissions
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        
        // Note: Content-Security-Policy should be configured per application needs
        // For API-only applications, we can be restrictive
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
        
        await _next(context);
    }
}
