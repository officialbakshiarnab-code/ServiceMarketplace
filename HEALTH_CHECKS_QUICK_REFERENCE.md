# Health Checks - Quick Reference

**Build Status**: ? Successful  
**Feature**: Comprehensive health monitoring

---

## Endpoints

| Endpoint | Purpose | Use Case |
|----------|---------|----------|
| `/health` | Detailed health status | Monitoring dashboards |
| `/health/ready` | Readiness probe | Kubernetes readiness |
| `/health/live` | Liveness probe | Kubernetes liveness |

---

## Health Checks

| Check | What It Verifies | Tags |
|-------|------------------|------|
| **database** | SQL Server connection | db, sql, ready |
| **auth_subsystem** | Identity & roles | auth, identity, ready |
| **background_jobs** | Cleanup services | jobs, background, ready |

---

## Response Format

```json
{
  "status": "Healthy",
  "timestamp": "2025-02-01T14:30:00Z",
  "totalDuration": 125.5,
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": "Database is accessible",
      "duration": 45.2,
      "data": { "userCount": 150 }
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

## HTTP Status Codes

| Status | Code | Meaning |
|--------|------|---------|
| Healthy | 200 | All checks passed |
| Degraded | 200 | App functional, some issues |
| Unhealthy | 503 | Critical failure |

---

## Testing

### Quick Test
```bash
# Check health status
curl https://localhost:7147/health | jq '.status'

# Check specific check
curl https://localhost:7147/health | jq '.checks[] | select(.name=="database")'

# Check summary
curl https://localhost:7147/health | jq '.summary'
```

### Expected Output
```
Healthy
```

---

## Kubernetes Configuration

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  failureThreshold: 3
```

---

## Common Issues

| Problem | Check | Solution |
|---------|-------|----------|
| 503 Error | database | Check SQL Server running |
| Degraded | auth_subsystem | Verify roles exist |
| Missing services | background_jobs | Check DI registration |

---

## Files Created

1. `DatabaseHealthCheck.cs` - DB connectivity
2. `AuthSubsystemHealthCheck.cs` - Auth validation
3. `BackgroundJobsHealthCheck.cs` - Service registration
4. `HealthCheckResponseWriter.cs` - JSON formatter

## Files Modified

1. `Program.cs` - Health check registration

---

## Performance

- **Database Check**: 10-100ms
- **Auth Check**: 10-50ms
- **Background Jobs Check**: 1-5ms
- **Total**: 20-150ms

---

## Security

?? **Public Endpoints** (no authentication)
- Required for load balancers
- Required for Kubernetes
- Don't expose sensitive data

---

**Status**: ? Production Ready  
**Version**: 1.0  
**Last Updated**: February 2025
