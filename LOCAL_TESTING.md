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

## Start API And Web UI

The Web UI calls the API at `https://localhost:7147`. If the API is not running, browser login shows `Failed to fetch` or `ERR_CONNECTION_REFUSED`.

Start both apps and verify the API health endpoint:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-dev.ps1
```

Apply migrations first, then start both apps:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-dev.ps1 -ApplyMigrations
```

Start only the API:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-dev.ps1 -ApiOnly
```

If you are using Visual Studio to run the Web UI, keep `7241` free and start only the API from PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-dev.ps1 -ApiOnly
```

If Visual Studio reports `address already in use` for `7241`, stop the background Web UI process:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\stop-local-dev.ps1 -WebOnly
```

Local URLs:

- API health: `https://localhost:7147/health/ready`
- API Swagger: `https://localhost:7147/swagger`
- Web UI: `https://localhost:7241`

If HTTPS certificates are not trusted on a new machine:

```powershell
dotnet dev-certs https --trust
```

## Git Safety

Do not commit real connection strings, JWT signing keys, `.env` files, Visual Studio user files, logs, local reports, or generated local database artifacts. The setup script writes runtime values outside the repository.

## Common Startup Failures

- `JWT signing key is a placeholder`: run `scripts/setup-local-dev.ps1`, then restart Visual Studio or the terminal.
- `Connection string 'DefaultConnection' not found`: run `scripts/setup-local-dev.ps1` with the PostgreSQL credentials for your machine.
- Browser login says `Network error during login: TypeError: Failed to fetch`: start the API on `https://localhost:7147` with `scripts/start-local-dev.ps1`.
- Visual Studio says `Failed to bind to address https://127.0.0.1:7241`: another Web UI process is already running; stop it with `scripts/stop-local-dev.ps1 -WebOnly`.
- PostgreSQL authentication failure: rerun the setup script with the password used by your local database or recreate the local container with `-StartPostgresContainer`.
- Migration failure: verify PostgreSQL is running, then rerun with `-ApplyMigrations`.
