# CORS & HTTPS Hardening - Quick Reference

## ? What Was Implemented

### 1. **Environment-Aware CORS Configuration**
- ? Development: `localhost:7241`, `localhost:5241` allowed
- ? Production: Only specific domains from configuration
- ? No wildcard origins (`*`) in production
- ? Configuration validation on startup

### 2. **HTTPS Enforcement**
- ? Configurable HTTPS redirection (disabled in dev, enabled in prod)
- ? HSTS (HTTP Strict Transport Security) support
- ? 308 Permanent Redirect for HTTP?HTTPS

### 3. **Security Headers Middleware**
- ? X-Content-Type-Options: nosniff
- ? X-Frame-Options: DENY
- ? X-XSS-Protection: 1; mode=block
- ? Referrer-Policy: strict-origin-when-cross-origin
- ? Permissions-Policy: restrictive
- ? Content-Security-Policy: API-focused

### 4. **Secure Cookie Configuration**
- ? SameSite: Lax (OAuth compatible)
- ? Secure: Always in production
- ? HttpOnly: Always for Identity cookies

---

## ?? Files Modified/Created

### Created Files
1. **ServiceMarketplace.API/Configuration/SecurityConfiguration.cs**
   - `CorsSettings` class with validation
   - `SecuritySettings` class for HTTPS/HSTS

2. **ServiceMarketplace.API/Middleware/SecurityHeadersMiddleware.cs**
   - Adds security headers to all responses

3. **ServiceMarketplace.API/appsettings.Production.json**
   - Production-specific security configuration

4. **CORS_HTTPS_HARDENING_IMPLEMENTATION.md**
   - Complete implementation documentation

### Modified Files
1. **ServiceMarketplace.API/appsettings.json**
   - Added `Cors:AllowedOrigins` section
   - Added `Security` section

2. **ServiceMarketplace.API/Program.cs**
   - Reads CORS origins from configuration
   - Validates configuration on startup
   - Configures HTTPS/HSTS conditionally
   - Adds security headers middleware
   - Configures secure cookies

---

## ?? Configuration Reference

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
    "UseHsts": false,
    "HstsMaxAgeSeconds": 31536000
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
    "HstsMaxAgeSeconds": 31536000
  }
}
```

---

## ?? Deployment Checklist

### Before Deploying to Production

- [ ] **Update CORS Origins**
  ```json
  "Cors": {
    "AllowedOrigins": [
      "https://yourdomain.com",
      "https://www.yourdomain.com"
    ]
  }
  ```

- [ ] **Enable HTTPS Enforcement**
  ```json
  "Security": {
    "EnforceHttps": true,
    "UseHsts": true
  }
  ```

- [ ] **Update JWT Secret**
  ```json
  "Jwt": {
    "Key": "GENERATE_A_SECURE_32_BYTE_KEY_HERE"
  }
  ```

- [ ] **Update Connection String**
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-server;Database=ServiceMarketplaceDB;..."
  }
  ```

- [ ] **Configure SSL Certificate**
  - Ensure SSL/TLS certificate is installed
  - Verify HTTPS is working on port 443

- [ ] **Update Client URLs**
  - Update UI.Web to use production API URL
  - Update UI.MAUI to use production API URL

---

## ?? Testing & Verification

### Test CORS Configuration

#### Test Allowed Origin (Should Succeed)
```bash
curl -H "Origin: https://localhost:7241" \
     -H "Access-Control-Request-Method: POST" \
     -H "Access-Control-Request-Headers: Content-Type" \
     -X OPTIONS https://localhost:7147/api/auth/login -v
```

**Expected Response:**
```
< HTTP/1.1 204 No Content
< Access-Control-Allow-Origin: https://localhost:7241
< Access-Control-Allow-Methods: POST
< Access-Control-Allow-Headers: Content-Type
< Access-Control-Allow-Credentials: true
```

#### Test Disallowed Origin (Should Fail)
```bash
curl -H "Origin: https://evil.com" \
     -H "Access-Control-Request-Method: POST" \
     -X OPTIONS https://localhost:7147/api/auth/login -v
```

**Expected Response:**
```
< HTTP/1.1 204 No Content
(No Access-Control-Allow-Origin header - CORS will block in browser)
```

### Test HTTPS Redirection

#### Test HTTP Redirect (Production Only)
```bash
curl -I http://localhost:5147/api/health
```

**Expected Response (Production):**
```
HTTP/1.1 308 Permanent Redirect
Location: https://localhost:7147/api/health
```

**Expected Response (Development):**
```
HTTP/1.1 200 OK
(No redirect in development)
```

### Test Security Headers

```bash
curl -I https://localhost:7147/api/health
```

**Expected Response Headers:**
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), microphone=(), camera=()
Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload (production only)
```

### Test Cookie Security

1. **Login via Browser**
   - Open DevTools (F12) ? Network tab
   - Login to application
   - Find the Set-Cookie header

2. **Verify Cookie Attributes**
   ```
   Set-Cookie: .AspNetCore.Identity.Application=...; path=/; secure; samesite=lax; httponly
   ```

   Required attributes:
   - ? `secure` (HTTPS only in production)
   - ? `samesite=lax` (CSRF protection)
   - ? `httponly` (XSS protection)

---

## ?? Troubleshooting

### Issue: "No CORS origins configured" Error on Startup

**Cause:** Missing or empty `Cors:AllowedOrigins` in appsettings.json

**Solution:**
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:7241"
    ]
  }
}
```

### Issue: CORS Errors in Browser Console

**Error Message:**
```
Access to XMLHttpRequest at 'https://localhost:7147/api/auth/login' from origin 
'https://localhost:7241' has been blocked by CORS policy
```

**Possible Causes:**
1. Origin not in `AllowedOrigins` array
2. Typo in origin URL (check https vs http, port number)
3. Missing `AllowCredentials()` in CORS policy

**Solution:**
Check API startup logs for:
```
CORS origin allowed: https://localhost:7241
```

### Issue: Cookies Not Sent with Requests

**Cause:** Cookie security policy mismatch

**Solutions:**
1. **Development:** Ensure `EnforceHttps: false` to allow HTTP cookies
2. **Production:** Ensure client uses HTTPS to match `Secure` cookie flag
3. **Check SameSite:** Use `Lax` instead of `Strict` for OAuth compatibility

### Issue: HTTP Requests Not Redirecting to HTTPS

**Cause:** `EnforceHttps` is `false` in configuration

**Solution:** In production appsettings:
```json
{
  "Security": {
    "EnforceHttps": true
  }
}
```

### Issue: Wildcard Origin Error

**Error Message:**
```
Wildcard CORS origin '*.example.com' is not allowed in production
```

**Cause:** Using wildcard in `AllowedOrigins`

**Solution:** Specify exact origins:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.example.com",
      "https://admin.example.com"
    ]
  }
}
```

---

## ?? Security Headers Explained

### X-Content-Type-Options: nosniff
**Purpose:** Prevents MIME type sniffing  
**Attack Prevented:** Executing malicious scripts disguised as images/other files

### X-Frame-Options: DENY
**Purpose:** Prevents page from being displayed in iframe  
**Attack Prevented:** Clickjacking attacks

### X-XSS-Protection: 1; mode=block
**Purpose:** Enables browser XSS filter  
**Attack Prevented:** Reflected XSS attacks (legacy browsers)

### Referrer-Policy: strict-origin-when-cross-origin
**Purpose:** Controls referrer information sent to other sites  
**Benefit:** Privacy protection, prevents information leakage

### Permissions-Policy
**Purpose:** Disables unnecessary browser features  
**Benefit:** Reduces attack surface

### Content-Security-Policy
**Purpose:** Restricts content sources  
**Benefit:** Prevents XSS and data injection attacks

### Strict-Transport-Security (HSTS)
**Purpose:** Forces HTTPS for specified duration  
**Benefit:** Prevents protocol downgrade attacks

---

## ?? Security Best Practices Applied

### 1. Defense in Depth
Multiple layers of security:
- CORS restriction
- HTTPS enforcement
- Security headers
- Secure cookies

### 2. Principle of Least Privilege
- Only specific origins allowed
- Only necessary permissions granted
- Restrictive CSP policy

### 3. Secure by Default
- Production settings prioritize security
- Development settings allow debugging
- Invalid configuration prevents startup

### 4. Configuration over Code
- Environment-specific settings in appsettings
- No hardcoded URLs or secrets
- Easy to update without code changes

---

## ?? Monitoring & Logging

### Startup Logs to Check

```
info: ServiceMarketplace.API.Program[0]
      CORS allowed origins: https://localhost:7241, http://localhost:5241
info: ServiceMarketplace.API.Program[0]
      HTTPS enforcement: False
info: ServiceMarketplace.API.Program[0]
      HSTS enabled: False
info: ServiceMarketplace.API.Program[0]
      CORS origin allowed: https://localhost:7241
info: ServiceMarketplace.API.Program[0]
      CORS origin allowed: http://localhost:5241
```

### Runtime Logs to Monitor

- CORS preflight rejections
- HTTPS redirect attempts
- Authentication cookie issues

---

## ?? Summary

| Feature | Development | Production |
|---------|-------------|------------|
| **CORS Origins** | localhost:7241, localhost:5241 | Your domain(s) |
| **HTTPS Enforcement** | ? Disabled | ? Enabled |
| **HSTS** | ? Disabled | ? Enabled |
| **Security Headers** | ? Enabled | ? Enabled |
| **Secure Cookies** | SameAsRequest | Always |
| **Cookie SameSite** | Lax | Lax |

---

## ?? Related Documentation

- [CORS_HTTPS_HARDENING_IMPLEMENTATION.md](./CORS_HTTPS_HARDENING_IMPLEMENTATION.md) - Full implementation details
- [Microsoft Docs - CORS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [Microsoft Docs - Enforce HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl)
- [OWASP - Secure Headers Project](https://owasp.org/www-project-secure-headers/)

---

**Implementation Date:** 2025-02-01  
**Status:** ? Complete  
**Build Status:** ? Successful  
**Security Level:** Production-Grade

