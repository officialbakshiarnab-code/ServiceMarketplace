# Database Setup

ServiceMarketplace now targets PostgreSQL through EF Core and Npgsql.

## Local Postgres

Start a local Postgres container:

```powershell
docker run --name service-marketplace-postgres `
  -e POSTGRES_USER=servicemarketplace `
  -e POSTGRES_PASSWORD=<local-dev-password> `
  -e POSTGRES_DB=service_marketplace `
  -p 5432:5432 `
  -d postgres:16
```

Default local connection string:

```text
Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=<local-dev-password>
```

Store the connection string outside source control:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=<local-dev-password>" --project ServiceMarketplace.API
dotnet user-secrets set "Jwt:Key" "replace-with-a-strong-32-byte-minimum-secret" --project ServiceMarketplace.API
```

Use environment variables for hosted or shared environments. `ServiceMarketplace.API/appsettings.json` intentionally contains placeholders and the API fails fast if the JWT key or connection string is missing.

## One-Shot Setup SQL

The root `full_migrations.sql` file is generated from EF Core migrations and can create all required tables, indexes, foreign keys, EF migration history, default role seed data, service categories, service zones, provider coverage fields, bid comparison fields, fixed-price service packages, service order lifecycle fields, order messages, notification inbox records, payment records, order reviews, service order audit events, seller profiles, product categories, product inspection prompts, product listings, and used-product disclosure fields.

Run it with `psql`:

```powershell
psql "host=localhost port=5432 dbname=service_marketplace user=servicemarketplace password=<local-dev-password>" -f .\full_migrations.sql
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
- `ServiceCategories`
- `ServiceZones`
- `Bids`
- `ServicePackages`
- `ServiceOrders`
- `ServiceOrderMessages`
- `ServiceOrderPayments`
- `ServiceOrderReviews`
- `ServiceOrderAuditEvents`
- `UserNotifications`
- `AuditLogs`
- `RefreshTokens`
- `ServiceProviderProfiles`
- `SellerProfiles`
- `ProductCategories`
- `ProductInspectionPrompts`
- `ProductListings`

Default seeded roles:

- `User`
- `ServiceProvider`
- `Both`
- `Admin`

Default seeded service categories:

- `Plumbing`
- `Electrical`
- `Cleaning`

Default seeded service zones:

- `Central Kolkata, Kolkata, West Bengal`
- `South Kolkata, Kolkata, West Bengal`
- `North Kolkata, Kolkata, West Bengal`

Default seeded product categories:

- `Home Essentials`
- `Tools And Hardware`
- `Electronics`

Default seeded used-product inspection prompts:

- Home Essentials visible wear, stains, cracks, missing parts, and cleaning status.
- Tools And Hardware rust, grip, moving parts, safety guards, serial/model labels, and accessories.
- Electronics power, battery/charging, ports, display, buttons, invoice/warranty, and reset/lock status.
