# ServiceMarketplace

A full-stack service marketplace platform that connects customers seeking services with providers offering specialized expertise. Built with .NET 9, Clean Architecture, PostgreSQL, and JWT-based authentication.

## Features

- Customer, service provider, both-role, and admin account types
- Service request lifecycle from posting through bidding and completion
- JWT authentication with refresh token rotation and revocation tracking
- Role-based API authorization policies
- Auth/session audit logging
- Provider age validation and optional government ID verification
- Provider application/profile lifecycle with admin review
- Service catalog categories, service zones, urgency, preferred schedule, and request requirements
- Provider request matching by approved category/zone coverage
- Bid comparison with provider profile context and estimated duration
- Service order/job lifecycle after bid acceptance
- Order-scoped customer/provider messaging after bid acceptance
- Persistent notification inbox for bid, order lifecycle, and message events
- Offline/direct payment recording before customer completion
- Transaction-backed customer reviews with provider rating aggregates
- Append-only service order transaction audit history
- Admin review moderation API
- Commercial UX polish for payment-first completion, provider transaction visibility, bid comparison ratings, and review moderation
- Fixed-price service packages with provider package management and customer booking
- Seller onboarding and admin approval for product-selling capability
- Product catalog categories and seller-managed product listings for buyers to browse
- Used product condition disclosure and category-specific inspection guidance
- Product delivery orders with buyer/seller tracking, stock reservation, cancellation, and seller fulfillment status updates
- Platform payment intent, admin verification, provider payout, and service-order dispute foundations
- Unified marketplace search across fixed-price service packages and product listings
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

### One-command Local Setup

For local development, run the setup script from the repository root. It writes JWT and database settings outside source control.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-dev.ps1
```

If Docker Desktop is installed and you want the script to create the local PostgreSQL container and apply migrations:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-dev.ps1 -StartPostgresContainer -ApplyMigrations
```

See `LOCAL_TESTING.md` for troubleshooting and custom PostgreSQL credentials.

To start both the API and Web UI after setup:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-dev.ps1
```

The Web UI runs at `https://localhost:7241` and calls the API at `https://localhost:7147`.

### Configuration

`ServiceMarketplace.API/appsettings.json` intentionally keeps secrets and local connection strings out of source control. Configure them with user secrets for local development:

```bash
dotnet user-secrets set "Jwt:Key" "replace-with-a-strong-32-byte-minimum-secret" --project ServiceMarketplace.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=<local-dev-password>" --project ServiceMarketplace.API
```

Or set equivalent environment variables in hosted environments:

```bash
Jwt__Key=replace-with-a-strong-32-byte-minimum-secret
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=<local-dev-password>
```

### Run Locally

```bash
dotnet restore

docker run --name service-marketplace-postgres \
  -e POSTGRES_USER=servicemarketplace \
  -e POSTGRES_PASSWORD=<local-dev-password> \
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
- Public registration allows only `User`, `ServiceProvider`, and `Both`; `Admin` accounts must be provisioned outside public registration.
- JWTs include marketplace capability claims such as `service.customer` and `service.provider`.
- Administrative access uses separate administrative permission claims such as `platform.admin`.
- Provider browsing and bidding require an active, KYC-approved provider account with provider capability.
- Provider accounts can also use service-customer workflows through the same account.
- Provider applications move through Draft, Submitted, Under Review, Approved, More Information Required, Rejected, Suspended, and Revoked states.
- Service request creation supports normalized service categories and service zones while retaining legacy text category/location compatibility.
- Provider-facing request listings hide exact customer location, coordinates, and customer IDs before bid acceptance.
- Accepted providers can view exact job location and coordinates for the accepted request.
- Accepted bids create service orders with Pending Start, In Progress, Provider Completed, Completed, and Cancelled states.
- Accepted order participants can message each other from their order pages.
- Notifications are created for accepted bids, order creation/start/completion/cancellation, and received order messages.
- Customer completion requires a recorded non-platform payment method for the agreed amount.
- Completed orders can receive one customer review, and provider average rating/review count are updated.
- Service order creation, lifecycle, payment, completion, review, and moderation events are retained in order audit history.
- Admins can hide/show customer reviews with moderation notes.
- Customer order screens guide payment recording before completion.
- Provider order screens show payment and review summaries.
- Bid comparison screens show provider rating, review count, rate, duration, rank, and bid message.
- Approved providers can publish fixed-price service packages for their approved category/zone.
- Customers can browse and book active packages, creating service orders without a bid.
- Users can apply to become sellers; admins review seller applications.
- Approved sellers receive `product.seller` capability and can manage product listings.
- Registered buyers can browse active product listings by category and Kolkata zone.
- Sellers can mark products as new or used; used products require condition notes and a seller inspection checklist.
- Product categories return inspection prompts so buyers know what to verify before pickup.
- Buyers can create product delivery orders from active listings; sellers confirm, ready, dispatch, deliver, or cancel orders.
- Product order creation reserves listing stock, and participant cancellation restores reserved stock before delivery starts.
- Customers can start a platform payment intent after provider completion; only admin/server-side verification creates a held platform payment.
- Released platform payments create pending provider payout records with platform fee and provider payout amounts.
- Participants can open disputes while payment is held; admins can refund the customer, release the provider, or reject the dispute.
- Users can search services and products from one marketplace surface with keyword, type, zone, category, condition, price, and sort filters.

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
They also cover provider application submission/review, capability claims, approval gates, catalog-backed request creation, provider matching, bid comparison, accepted-provider address disclosure, service order lifecycle flows, order messaging, notification inbox behavior, payment recording, completion hardening, transaction-backed reviews, review moderation, fixed-price service package booking, seller approval, product listing management, buyer product browsing, used-product condition disclosure, inspection guidance, product delivery order lifecycle behavior, platform payment verification, payout creation, dispute refunds, unified marketplace search, and the MVP Gate A full service transaction smoke path.

## Production Notes

- Set `Jwt:Key` to a strong production secret with at least 32 bytes.
- Update CORS origins to production domains.
- Enable HTTPS and HSTS in production.
- Configure PostgreSQL backups and monitoring.
- Review audit logs regularly.

See `TECHNICAL-README.md` for architecture details.
