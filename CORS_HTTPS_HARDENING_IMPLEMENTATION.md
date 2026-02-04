# CORS and HTTPS Hardening Implementation Plan

## ?? Objectives

1. **Restrict CORS** - No wildcard origins in production
2. **Enforce HTTPS** - Redirect all HTTP to HTTPS
3. **Secure Cookies** - SameSite, Secure, HttpOnly flags
4. **Environment-Aware Configuration** - Development vs Production settings
5. **Security Headers** - Add additional security headers

---

## ?? Security Requirements

### CORS Configuration
- ? Development: `localhost:7241`, `localhost:5241`
- ? Production: Specific domain(s) only (configurable)
- ? No `AllowAnyOrigin()` in production
- ? Validate origins from configuration

### HTTPS Configuration
- ? Force HTTPS redirection
- ? Use HSTS (HTTP Strict Transport Security)
- ? Configure HTTPS in production

### Cookie Security
- ? SameSite: Strict or Lax
- ? Secure: true (HTTPS only)
- ? HttpOnly: true (prevent XSS)

### Security Headers
- ? X-Content-Type-Options: nosniff
- ? X-Frame-Options: DENY
- ? X-XSS-Protection: 1; mode=block
- ? Referrer-Policy: strict-origin-when-cross-origin

---

## ?? Implementation Checklist

### 1. Add Configuration for Allowed Origins
- [ ] Add `AllowedOrigins` section to appsettings.json
- [ ] Add production-specific origins to appsettings.Production.json
- [ ] Validate configuration on startup

### 2. Update CORS Policy
- [ ] Read origins from configuration
- [ ] Remove hardcoded localhost origins
- [ ] Validate no wildcard origins
- [ ] Add origin validation logging

### 3. Configure HTTPS
- [ ] Enable HTTPS redirection
- [ ] Configure HSTS middleware
- [ ] Set secure cookie options

### 4. Add Security Headers Middleware
- [ ] Create SecurityHeadersMiddleware
- [ ] Add security headers to all responses
- [ ] Configure CSP (Content Security Policy)

### 5. Update Cookie Configuration
- [ ] Configure cookie security for authentication
- [ ] Set SameSite policy
- [ ] Enable Secure flag
- [ ] Enable HttpOnly flag

### 6. Testing
- [ ] Test CORS with allowed origins
- [ ] Test CORS rejection with disallowed origins
- [ ] Test HTTPS redirection
- [ ] Test cookie security flags
- [ ] Verify security headers in responses

---

## ?? Configuration Structure

### appsettings.json (Development)
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:7241",
      "http://localhost:5241"
    ]
  },
  "Security": {
    "EnforceHttps": false,
    "UseHsts": false
  }
}
```

### appsettings.Production.json (Production)
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://yourdomain.com",
      "https://www.yourdomain.com"
    ]
  },
  "Security": {
    "EnforceHttps": true,
    "UseHsts": true,
    "HstsMaxAge": 31536000
  }
}
```

---

## ?? Implementation Steps

### Step 1: Create Security Configuration Model
```csharp
public class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public class SecuritySettings
{
    public bool EnforceHttps { get; set; }
    public bool UseHsts { get; set; }
    public int HstsMaxAge { get; set; } = 31536000; // 1 year
}
```

### Step 2: Update Program.cs CORS Configuration
```csharp
// Read CORS settings from configuration
var corsSettings = builder.Configuration
    .GetSection("Cors")
    .Get<CorsSettings>() ?? new CorsSettings();

if (corsSettings.AllowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "No CORS origins configured. Add 'Cors:AllowedOrigins' to appsettings.json");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsSettings.AllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

### Step 3: Configure HTTPS and HSTS
```csharp
var securitySettings = builder.Configuration
    .GetSection("Security")
    .Get<SecuritySettings>() ?? new SecuritySettings();

if (securitySettings.EnforceHttps)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 7147; // Configure per environment
    });
}

if (securitySettings.UseHsts)
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromSeconds(securitySettings.HstsMaxAge);
        options.IncludeSubDomains = true;
        options.Preload = true;
    });
}
```

### Step 4: Configure Cookie Security
```csharp
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always; // HTTPS only
});
```

### Step 5: Add Security Headers Middleware
```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        
        await _next(context);
    }
}
```

---

## ?? Important Notes

### Development vs Production
- **Development**: Relaxed CORS, HTTP allowed, detailed errors
- **Production**: Strict CORS, HTTPS enforced, minimal error details

### Breaking Changes
- HTTP requests in production will be redirected to HTTPS
- Origins not in configuration will be rejected
- Cookies will only work over HTTPS in production

### Migration Path
1. Update configuration files
2. Deploy code changes
3. Update client applications to use HTTPS
4. Monitor logs for CORS rejections

---

## ?? Verification Checklist

### CORS Verification
```bash
# Test allowed origin
curl -H "Origin: https://localhost:7241" \
     -H "Access-Control-Request-Method: POST" \
     -H "Access-Control-Request-Headers: Content-Type" \
     -X OPTIONS https://localhost:7147/api/auth/login

# Expected: 200 OK with CORS headers

# Test disallowed origin
curl -H "Origin: https://evil.com" \
     -H "Access-Control-Request-Method: POST" \
     -X OPTIONS https://localhost:7147/api/auth/login

# Expected: CORS error (no Access-Control-Allow-Origin header)
```

### HTTPS Verification
```bash
# HTTP request should redirect to HTTPS
curl -I http://localhost:5147/api/health

# Expected: 308 Permanent Redirect to https://localhost:7147/api/health
```

### Security Headers Verification
```bash
curl -I https://localhost:7147/api/health

# Expected headers:
# X-Content-Type-Options: nosniff
# X-Frame-Options: DENY
# X-XSS-Protection: 1; mode=block
# Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

---

## ?? Security Best Practices Applied

1. ? **Principle of Least Privilege**: Only allow specific origins
2. ? **Defense in Depth**: Multiple security layers (CORS, HTTPS, headers)
3. ? **Secure by Default**: Production settings prioritize security
4. ? **Configuration over Code**: Environment-specific settings
5. ? **Fail Securely**: Invalid configuration prevents startup

---

## ?? Post-Implementation Tasks

- [ ] Update deployment documentation
- [ ] Configure production origins
- [ ] Test with production URLs
- [ ] Monitor CORS rejections in logs
- [ ] Review security headers with security team
- [ ] Update client applications for HTTPS

---

**Implementation Status**: Ready to Execute
**Security Level**: Production-Grade
**Breaking Changes**: Yes (requires configuration update)

