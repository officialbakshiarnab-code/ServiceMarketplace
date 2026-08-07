# Local Testing Readiness

This repository is designed to build, test, and run locally without committing real secrets or machine-specific configuration.

## Quick Setup

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-dev.ps1
```

This configures the API JWT settings and `ConnectionStrings:DefaultConnection` outside source control by using .NET user-secrets and user-level environment variables.

To start a local PostgreSQL container as well, install Docker Desktop and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-dev.ps1 -StartPostgresContainer -ApplyMigrations
```

If you already have PostgreSQL running with different credentials:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-dev.ps1 `
  -PostgresHost localhost `
  -PostgresPort 5432 `
  -Database service_marketplace `
  -Username servicemarketplace `
  -Password "your-local-password" `
  -ApplyMigrations
```

## Verify

```powershell
dotnet restore
dotnet build .\ServiceMarketplace.sln --no-restore
dotnet test .\ServiceMarketplace.sln --no-build
dotnet run --project .\ServiceMarketplace.API
```

Swagger is available at `https://localhost:7147/swagger` in Development.

## Git Safety

Do not commit real connection strings, JWT signing keys, `.env` files, Visual Studio user files, logs, local reports, or generated local database artifacts. The setup script writes runtime values outside the repository.

## Common Startup Failures

- `JWT signing key is a placeholder`: run `scripts/setup-local-dev.ps1`, then restart Visual Studio or the terminal.
- `Connection string 'DefaultConnection' not found`: run `scripts/setup-local-dev.ps1` with the PostgreSQL credentials for your machine.
- PostgreSQL authentication failure: rerun the setup script with the password used by your local database or recreate the local container with `-StartPostgresContainer`.
- Migration failure: verify PostgreSQL is running, then rerun with `-ApplyMigrations`.
