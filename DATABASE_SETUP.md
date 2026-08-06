# Database Setup

ServiceMarketplace now targets PostgreSQL through EF Core and Npgsql.

## Local Postgres

Start a local Postgres container:

```powershell
docker run --name service-marketplace-postgres `
  -e POSTGRES_USER=servicemarketplace `
  -e POSTGRES_PASSWORD=servicemarketplace_dev `
  -e POSTGRES_DB=service_marketplace `
  -p 5432:5432 `
  -d postgres:16
```

Default local connection string:

```text
Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=servicemarketplace_dev
```

Use user secrets or environment variables for non-local credentials.

## One-Shot Setup SQL

The root `full_migrations.sql` file is generated from EF Core migrations and can create all required tables, indexes, foreign keys, EF migration history, and default role seed data.

Run it with `psql`:

```powershell
psql "host=localhost port=5432 dbname=service_marketplace user=servicemarketplace password=servicemarketplace_dev" -f .\full_migrations.sql
```

Or apply migrations through EF:

```powershell
dotnet ef database update --project ServiceMarketplace.Infrastructure --startup-project ServiceMarketplace.API
```

## Regenerate Migrations And SQL

After changing `AppDbContext` or domain entities:

```powershell
dotnet ef migrations add <MigrationName> --project ServiceMarketplace.Infrastructure --startup-project ServiceMarketplace.API --output-dir Data\Migrations
dotnet ef migrations script --project ServiceMarketplace.Infrastructure --startup-project ServiceMarketplace.API --idempotent --output full_migrations.sql
```

## Current Tables

- `Users`
- `Roles`
- `UserRoles`
- `ServiceRequests`
- `Bids`
- `AuditLogs`
- `RefreshTokens`

Default seeded roles:

- `User`
- `ServiceProvider`
- `Both`
- `Admin`
