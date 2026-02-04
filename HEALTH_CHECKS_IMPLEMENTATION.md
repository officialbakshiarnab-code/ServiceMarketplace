# Health Checks - Implementation Complete

**Date**: February 2025  
**Status**: ? **BUILD SUCCESSFUL**  
**Feature**: Comprehensive health monitoring with database, auth, and background job checks

---

## Executive Summary

Health checks have been successfully implemented with:

? **Database connectivity check** - Verifies SQL Server connection and responsiveness  
? **Auth subsystem check** - Validates ASP.NET Identity and role management  
? **Background jobs check** - Ensures cleanup services are registered  
? **/health endpoint** - Detailed health information in JSON format  
? **/health/ready endpoint** - Kubernetes readiness probe  
? **/health/live endpoint** - Kubernetes liveness probe  
? **UI-friendly response** - Pretty-printed JSON with detailed metrics  

---

## Health Check Endpoints

### 1. ? /health (Detailed Health Check)

**Purpose**: Comprehensive health status with all details

**Response Format**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-02-01T14:30:00Z",
  "totalDuration": 125.5,
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": "Database is accessible and responsive",
      "duration": 45.2,
      "exception": null,
      "data": {
        "userCount": 150,
        "connectionString": "Data Source=localhost"
      }
    },
    {
      "name": "auth_subsystem",
      "status": "Healthy",
      "description": "Authentication subsystem is healthy",
      "duration": 35.8,
      "exception": null,
      "data": {
        "rolesCount": 3,
        "usersCount": 150,
        "identityVersion": "9.0.0"
      }
    },
    {
      "name": "background_jobs",
      "status": "Healthy",
      "description": "All background jobs are registered",
      "duration": 2.1,
      "exception": null,
      "data": {
        "totalHostedServices": 3,
        "cleanupServicesCount": 3,
        "registeredServices": "RefreshTokenCleanupService, AbandonedSessionCleanupService, AuditLogArchivalService"
      }
    }
  ],
  "summary": {
    "total": 3,
    "healthy": 3,
    "degraded": 0,
    "unhealthy": 0
  }
}
```

**HTTP Status Codes**:
- `200 OK`: All checks are Healthy
- `200 OK`: Some checks are Degraded (app still functional)
- `503 Service Unavailable`: One or more checks are Unhealthy

**Use Cases**:
- Monitoring dashboards
- Health aggregation tools
- Debugging issues
- Status pages

---

### 2. ? /health/ready (Readiness Probe)

**Purpose**: Check if app is ready to accept traffic (Kubernetes readiness probe)

**Response**: Same format as `/health` but only includes checks tagged with "ready"

**Checks Included**:
- ? Database connectivity
- ? Auth subsystem
- ? Background jobs

**HTTP Status Codes**:
- `200 OK`: App is ready to accept traffic
- `503 Service Unavailable`: App is not ready (startup still in progress or critical failure)

**Use Cases**:
- Kubernetes readiness probe
- Load balancer health checks
- Rolling deployment verification

**Kubernetes Configuration**:
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 3
```

---

### 3. ? /health/live (Liveness Probe)

**Purpose**: Check if app is alive and should stay running (Kubernetes liveness probe)

**Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-02-01T14:30:00Z",
  "totalDuration": 0.1,
  "checks": [],
  "summary": {
    "total": 0,
    "healthy": 0,
    "degraded": 0,
    "unhealthy": 0
  }
}
```

**HTTP Status Codes**:
- `200 OK`: App is alive (process is running)

**Use Cases**:
- Kubernetes liveness probe
- Process monitoring
- Container orchestration

**Kubernetes Configuration**:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 3
  failureThreshold: 3
```

**Note**: This endpoint doesn't run any checks - it just returns 200 OK if the app is running. This prevents Kubernetes from restarting the pod due to temporary issues with dependencies.

---

## Health Checks Implemented

### 1. ? Database Health Check

**Class**: `DatabaseHealthCheck`

**What It Checks**:
- Database connection (`CanConnectAsync`)
- Database responsiveness (simple `CountAsync` query)
- User table accessibility

**Health States**:
- **Healthy**: Database is accessible and responsive
- **Unhealthy**: Cannot connect or query fails

**Data Returned**:
```json
{
  "userCount": 150,
  "connectionString": "Data Source=localhost"
}
```

**Common Issues**:
- Database server is down
- Connection string is incorrect
- Network issues
- Database credentials invalid
- Database is locked or unresponsive

---

### 2. ? Auth Subsystem Health Check

**Class**: `AuthSubsystemHealthCheck`

**What It Checks**:
- ASP.NET Identity is functioning
- Roles are configured (User, ServiceProvider, Admin)
- Users exist in the system
- UserManager and RoleManager are accessible

**Health States**:
- **Healthy**: Roles configured (?2) and users exist
- **Degraded**: Identity is functioning but roles/users may not be fully configured
- **Unhealthy**: Identity subsystem failure

**Data Returned**:
```json
{
  "rolesCount": 3,
  "usersCount": 150,
  "identityVersion": "9.0.0"
}
```

**Common Issues**:
- No roles seeded
- Identity tables missing
- Database migration not applied
- Identity configuration error

---

### 3. ? Background Jobs Health Check

**Class**: `BackgroundJobsHealthCheck`

**What It Checks**:
- Background services are registered
- Expected cleanup services exist:
  - RefreshTokenCleanupService
  - AbandonedSessionCleanupService
  - AuditLogArchivalService

**Health States**:
- **Healthy**: All expected background services are registered
- **Degraded**: Some expected services are missing
- **Unhealthy**: Health check execution failed

**Data Returned**:
```json
{
  "totalHostedServices": 3,
  "cleanupServicesCount": 3,
  "registeredServices": "RefreshTokenCleanupService, AbandonedSessionCleanupService, AuditLogArchivalService"
}
```

**Common Issues**:
- Background services not registered in DI
- Service registration error
- Missing dependencies

---

## Response Format

### UI-Friendly JSON

**Features**:
- ? Pretty-printed (indented)
- ? Camel case property names
- ? Human-readable descriptions
- ? Duration in milliseconds
- ? Exception messages (if any)
- ? Custom data from each check
- ? Summary statistics

**Custom Response Writer**: `HealthCheckResponseWriter`

**Benefits**:
- Easy to read in browser
- Easy to parse for monitoring tools
- Provides detailed diagnostics
- Includes timing information

---

## Configuration

### Health Check Registration

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "database",
        tags: new[] { "db", "sql", "ready" })
    .AddCheck<AuthSubsystemHealthCheck>(
        "auth_subsystem",
        tags: new[] { "auth", "identity", "ready" })
    .AddCheck<BackgroundJobsHealthCheck>(
        "background_jobs",
        tags: new[] { "jobs", "background", "ready" });
```

**Tags**:
- `ready`: Included in readiness probe
- `live`: Included in liveness probe
- `db`, `sql`, `auth`, `identity`, `jobs`, `background`: Descriptive tags for filtering

---

### Endpoint Mapping

```csharp
// Detailed health check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    AllowCachingResponses = false
});

// Readiness probe
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    AllowCachingResponses = false
});

// Liveness probe
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // No checks, just 200 OK
    AllowCachingResponses = false
});
```

**AllowCachingResponses**: Set to `false` to ensure fresh health status on every request

---

## Testing

### Test 1: Verify All Checks Pass

**Steps**:
1. Start API application
2. Navigate to `https://localhost:7147/health`

**Expected Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-02-01T14:30:00Z",
  "totalDuration": 125.5,
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      ...
    },
    {
      "name": "auth_subsystem",
      "status": "Healthy",
      ...
    },
    {
      "name": "background_jobs",
      "status": "Healthy",
      ...
    }
  ],
  "summary": {
    "total": 3,
    "healthy": 3,
    "degraded": 0,
    "unhealthy": 0
  }
}
```

---

### Test 2: Simulate Database Failure

**Steps**:
1. Stop SQL Server
2. Navigate to `https://localhost:7147/health`

**Expected Response**:
```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "database",
      "status": "Unhealthy",
      "description": "Cannot connect to database",
      "exception": "A network-related or instance-specific error...",
      ...
    },
    ...
  ],
  "summary": {
    "healthy": 0,
    "unhealthy": 1
  }
}
```

**HTTP Status**: `503 Service Unavailable`

---

### Test 3: Verify Readiness Probe

**Steps**:
1. Navigate to `https://localhost:7147/health/ready`

**Expected Response**: Only checks tagged with "ready" (all 3 checks)

**Use Case**: Kubernetes will not route traffic to pod until this returns 200 OK

---

### Test 4: Verify Liveness Probe

**Steps**:
1. Navigate to `https://localhost:7147/health/live`

**Expected Response**:
```json
{
  "status": "Healthy",
  "checks": [],
  "summary": {
    "total": 0
  }
}
```

**HTTP Status**: `200 OK` (always, as long as app is running)

**Use Case**: Kubernetes will restart pod if this doesn't return 200 OK

---

## Monitoring Integration

### Azure Application Insights

```csharp
// Add to Program.cs
builder.Services.AddHealthChecks()
    .AddApplicationInsightsPublisher();
```

**Features**:
- Automatic health check metrics
- Alerts on unhealthy status
- Historical health data

---

### Prometheus

```csharp
// Install: AspNetCore.HealthChecks.Publisher.Prometheus
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .ForwardToPrometheus();
```

**Features**:
- Prometheus metrics endpoint
- Grafana dashboard integration
- Time-series health data

---

### Custom Monitoring

**Query /health endpoint**:
```bash
curl -s https://api.example.com/health | jq '.status'
```

**Parse JSON response**:
```bash
#!/bin/bash
HEALTH_STATUS=$(curl -s https://api.example.com/health | jq -r '.status')

if [ "$HEALTH_STATUS" != "Healthy" ]; then
  echo "Alert: API is $HEALTH_STATUS"
  # Send notification
fi
```

---

## Performance Considerations

### Execution Time

**Typical Response Times**:
- Database check: 10-100ms (depends on database load)
- Auth subsystem check: 10-50ms (depends on database load)
- Background jobs check: 1-5ms (in-memory check)
- **Total**: 20-150ms

**Optimization Tips**:
- Keep health checks lightweight
- Use simple queries (no complex JOINs)
- Cache results if checks are expensive
- Use timeouts to prevent hanging

---

### Caching

**Current**: No caching (`AllowCachingResponses = false`)

**Reason**: Health status should always be fresh

**Alternative** (if needed):
```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    AllowCachingResponses = true,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
```

---

## Security Considerations

### Public Endpoint

?? **Health endpoints are public** (no authentication required)

**Why?**
- Load balancers need access
- Kubernetes probes don't support auth
- Monitoring tools need access

**Mitigation**:
- Don't expose sensitive data in health checks
- Don't include connection strings (only show server name)
- Don't include passwords
- Sanitize error messages

---

### Data Exposure

**What's Included**:
- ? User count (public metric)
- ? Role count (public metric)
- ? Connection server name (not full connection string)
- ? Service names (public info)

**What's NOT Included**:
- ? Passwords
- ? Connection strings with credentials
- ? API keys
- ? User emails or personal data

---

### Rate Limiting

**Consider adding rate limiting** for health endpoints:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("health", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60, // 60 requests per minute
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

app.MapHealthChecks("/health", ...)
   .RequireRateLimiting("health");
```

---

## Customization

### Add Custom Health Check

```csharp
public class CustomHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Your custom health check logic
        var isHealthy = await CheckSomethingAsync();

        if (isHealthy)
        {
            return HealthCheckResult.Healthy(
                "Custom check passed",
                data: new Dictionary<string, object>
                {
                    { "customMetric", 42 }
                });
        }

        return HealthCheckResult.Unhealthy("Custom check failed");
    }
}

// Register in Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom_check");
```

---

### Add External Service Check

**Example: Check external API**:
```csharp
public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public ExternalApiHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "https://external-api.example.com/health",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("External API is reachable");
            }

            return HealthCheckResult.Degraded(
                $"External API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "External API is unreachable",
                exception: ex);
        }
    }
}
```

---

## Kubernetes Deployment

### Full Example

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: service-marketplace-api
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: api
        image: service-marketplace-api:latest
        ports:
        - containerPort: 8080
        
        # Liveness probe: Restart if unhealthy
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
            scheme: HTTP
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 3
          failureThreshold: 3
          successThreshold: 1
        
        # Readiness probe: Route traffic only when ready
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
            scheme: HTTP
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
          successThreshold: 1
```

---

## File Changes Summary

### New Files Created (4)

1. **ServiceMarketplace.API\HealthChecks\DatabaseHealthCheck.cs** - Database connectivity check
2. **ServiceMarketplace.API\HealthChecks\AuthSubsystemHealthCheck.cs** - Auth subsystem check
3. **ServiceMarketplace.API\HealthChecks\BackgroundJobsHealthCheck.cs** - Background jobs check
4. **ServiceMarketplace.API\HealthChecks\HealthCheckResponseWriter.cs** - UI-friendly response writer

### Files Modified (1)

1. **ServiceMarketplace.API\Program.cs** - Registered health checks and mapped endpoints

---

## Build Status

? **BUILD SUCCESSFUL** - 0 errors, 0 warnings

---

## Future Enhancements (Optional)

### 1. Health Check UI

Install `AspNetCore.HealthChecks.UI`:
```csharp
builder.Services.AddHealthChecksUI();

app.MapHealthChecksUI(options => 
{
    options.UIPath = "/health-ui";
});
```

**Features**:
- Web-based dashboard
- Historical health data
- Alerts and notifications

---

### 2. Response Caching

```csharp
builder.Services.AddResponseCaching();
app.UseResponseCaching();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    AllowCachingResponses = true
}).CacheOutput(builder => builder.Expire(TimeSpan.FromSeconds(30)));
```

---

### 3. Detailed Degradation Reasons

Add more granular degradation states:
```csharp
if (userCount == 0)
{
    return HealthCheckResult.Degraded(
        "No users in system - may need data seeding",
        data: healthData);
}

if (roleCount < 3)
{
    return HealthCheckResult.Degraded(
        "Missing expected roles - Admin role not configured",
        data: healthData);
}
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| 503 on /health | Check which check is failing in response JSON |
| Database check fails | Verify SQL Server is running and connection string is correct |
| Auth check fails | Ensure Identity tables exist and migrations are applied |
| Background jobs check fails | Verify hosted services are registered in Program.cs |
| Timeout on health check | Increase timeout or optimize check query |

---

## Conclusion

? **IMPLEMENTATION COMPLETE**

Health checks provide:
- ? Real-time system health monitoring
- ? Kubernetes integration (readiness/liveness probes)
- ? Detailed diagnostics for troubleshooting
- ? UI-friendly JSON responses
- ? Multiple endpoints for different use cases

**Build Status**: ? Successful  
**Endpoints**: ? /health, /health/ready, /health/live  
**Checks**: ? Database, Auth, Background Jobs  
**Kubernetes**: ? Ready for deployment  

---

**Document Version**: 1.0  
**Last Updated**: February 2025  
**Status**: ? PRODUCTION READY
