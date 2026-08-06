# ServiceMarketplace

A full-stack service marketplace platform that connects customers seeking services with providers offering specialized expertise. Built with .NET 9, Clean Architecture, PostgreSQL, and JWT-based authentication.

## Features

- Customer, service provider, both-role, and admin account types
- Service request lifecycle from posting through bidding and completion
- JWT authentication with refresh token rotation and revocation tracking
- Role-based API authorization policies
- Auth/session audit logging
- Provider age validation and optional government ID verification
- API rate limiting, security headers, and health checks

## Architecture

| Layer | Responsibility |
|-------|----------------|
| API | ASP.NET Core Web API, controllers, middleware, health checks |
| Application | DTOs, validators, service interfaces, exceptions |
| Domain | Core entities, enums, and domain contracts |
| Infrastructure | EF Core, PostgreSQL migrations, service implementations, background jobs |
| UI | Blazor WebAssembly, MAUI Hybrid, and shared Razor components |

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend API | ASP.NET Core 9.0 |
| Database | PostgreSQL via EF Core Npgsql |
| Authentication | Custom auth tables + JWT |
| Web UI | Blazor WebAssembly (.NET 9) |
| Mobile UI | .NET MAUI Hybrid |
| Shared Components | Razor Components |
| Architecture | Clean Architecture |

## Getting Started

### Prerequisites

- .NET 9 SDK
- PostgreSQL 16+ local instance, Docker container, or cloud database
- Visual Studio 2022+ or VS Code with C# extensions

### Configuration

`ServiceMarketplace.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=servicemarketplace_dev"
  },
  "Jwt": {
    "Key": "your-256-bit-secret-key-here",
    "Issuer": "ServiceMarketplace",
    "Audience": "ServiceMarketplaceUsers"
  }
}
```

### Run Locally

```bash
dotnet restore

docker run --name service-marketplace-postgres \
  -e POSTGRES_USER=servicemarketplace \
  -e POSTGRES_PASSWORD=servicemarketplace_dev \
  -e POSTGRES_DB=service_marketplace \
  -p 5432:5432 \
  -d postgres:16

dotnet ef database update --project ServiceMarketplace.Infrastructure --startup-project ServiceMarketplace.API
dotnet run --project ServiceMarketplace.API
```

API documentation is available at `https://localhost:7147/swagger` when the API runs in Development.

## Database Setup

Use `DATABASE_SETUP.md` for local PostgreSQL setup and `full_migrations.sql` for a one-shot database initialization script.

## Authentication

- Access tokens expire after 10 minutes.
- Refresh tokens expire after 7 days and rotate on refresh.
- Refresh tokens are stored hashed in the database.
- Session IDs are tracked through JWT `jti` claims and audit logs.

## Project Structure

```text
ServiceMarketplace/
|-- ServiceMarketplace.API/
|-- ServiceMarketplace.Application/
|-- ServiceMarketplace.Domain/
|-- ServiceMarketplace.Infrastructure/
|-- ServiceMarketplace.UI.Web/
|-- ServiceMarketplace.UI.MAUI/
|-- ServiceMarketplace.UI.Shared/
`-- ServiceMarketplace.Shared/
```

## Testing

```bash
dotnet build ServiceMarketplace.sln --no-restore
dotnet test ServiceMarketplace.sln --no-build
```

Integration tests cover registration, login, token refresh, authorization, bids, and request workflows.

## Production Notes

- Set `Jwt:Key` to a strong production secret.
- Update CORS origins to production domains.
- Enable HTTPS and HSTS in production.
- Configure PostgreSQL backups and monitoring.
- Review audit logs regularly.

See `TECHNICAL-README.md` for architecture details.
