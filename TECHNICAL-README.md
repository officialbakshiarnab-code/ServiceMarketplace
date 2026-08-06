# Technical Architecture Guide

This document explains the internal architecture, design decisions, and implementation patterns used in ServiceMarketplace.

## Architecture Overview

ServiceMarketplace follows Clean Architecture with four main code layers and separate UI projects.

```
UI Layer (Blazor WASM + MAUI)        ServiceMarketplace.UI.*
  - Razor components
  - Authentication state provider
  - API client wrappers
        |
        | HTTP + JWT
        v
API Layer (ASP.NET Core)             ServiceMarketplace.API
  - Controllers
  - Middleware
  - Health checks
  - Rate limiting policies
        |
        | Dependency Injection
        v
Application Layer                    ServiceMarketplace.Application
  - DTOs and validators
  - Service contracts
  - Application exceptions
        |
        v
Domain Layer                         ServiceMarketplace.Domain
  - Entities and enums
  - Domain-level contracts
        |
        v
Infrastructure Layer                 ServiceMarketplace.Infrastructure
  - EF Core DbContext
  - Custom auth services
  - Background services
  - Postgres migrations
        |
        | EF Core + Npgsql
        v
PostgreSQL Database
  - Users, Roles, UserRoles
  - ServiceRequests, Bids
  - AuditLogs
  - RefreshTokens
```

## Dependency Rules

- API depends on Application, Domain, and Infrastructure for composition.
- Application defines service contracts and validation behavior.
- Domain stays independent from infrastructure and API concerns.
- Infrastructure owns persistence, external service implementations, and migrations.

## Authentication & Authorization

ServiceMarketplace uses custom auth tables with JWT access tokens and refresh token rotation.

### Login Flow

1. User submits an email or phone identifier with password.
2. Server validates credentials against `Users`.
3. Server issues a short-lived JWT access token and a 7-day refresh token.
4. Refresh tokens are stored hashed in `RefreshTokens` and rotated on use.
5. Logout revokes active refresh tokens for the user.

### Authorization

Authorization policies are based on the `UserType` claim and custom roles.

```csharp
[Authorize(Policy = "UserOnly")]
public async Task<IActionResult> CreateRequest(...)

[Authorize(Policy = "ProviderOnly")]
public async Task<IActionResult> SubmitBid(...)

[Authorize(Policy = "AdminOnly")]
public async Task<IActionResult> GetAuditLogs(...)
```

## Database Model

PostgreSQL is the runtime database. EF Core migrations live under `ServiceMarketplace.Infrastructure/Data/Migrations`, and the generated one-shot SQL setup script is `full_migrations.sql`.

Core tables:

| Table | Purpose |
|-------|---------|
| `Users` | Custom account profile, password hash, verification, lockout, and KYC state |
| `Roles` | Seeded marketplace role names: User, ServiceProvider, Both, Admin |
| `UserRoles` | Many-to-many user/role mapping |
| `ServiceRequests` | Customer-created service request records |
| `Bids` | Provider bids linked to service requests |
| `AuditLogs` | Registration, login, logout, session, and admin audit events |
| `RefreshTokens` | Hashed refresh tokens, token family tracking, revocation, and expiry |

Important relationships:

- `ServiceRequests` has many `Bids`.
- `Users` has many `UserRoles`; `Roles` has many `UserRoles`.
- `AuditLogs` and `RefreshTokens` store user IDs for auth/session tracking.

## Business Rules

- Customer users can post service requests.
- Service providers can browse requests and submit bids.
- Both-role users can act as customer and provider.
- Admin users can access operational and audit endpoints.
- Provider and Both registrations require age validation for 18+.
- Government ID upload is optional and stored as a file path when submitted.

## Security Implementation

- JWT access tokens expire after 10 minutes.
- Refresh tokens expire after 7 days and are rotated on refresh.
- Passwords are hashed with PBKDF2 before storage.
- Rate limits protect auth, refresh, bid, request, and admin endpoints.
- Security headers are applied through middleware.
- Audit logging records key auth and session events.

## Background Jobs

- `RefreshTokenCleanupService` removes expired refresh tokens.
- `AbandonedSessionCleanupService` handles stale auth sessions.
- `AuditLogArchivalService` maintains audit log retention.

## Testing Strategy

Authentication and authorization are primarily covered by integration tests because token, persistence, and request pipeline behavior must work together.

Run validation:

```bash
dotnet build ServiceMarketplace.sln --no-restore
dotnet test ServiceMarketplace.sln --no-build
```

The test factory uses EF Core in-memory storage for integration tests. Local development uses PostgreSQL configured by `appsettings.json` and `DATABASE_SETUP.md`.

## Development Workflow

1. Update domain entities in `ServiceMarketplace.Domain`.
2. Update contracts or validators in `ServiceMarketplace.Application`.
3. Update persistence and migrations in `ServiceMarketplace.Infrastructure`.
4. Update controllers or middleware in `ServiceMarketplace.API`.
5. Run build and tests.
6. Regenerate `full_migrations.sql` when migrations change.

## References

- Local database setup: `DATABASE_SETUP.md`
- One-shot database script: `full_migrations.sql`
- ProjectBrain DB architecture notes: `ProjectBrain/projects/ServiceMarketplace/DATABASE.md`
