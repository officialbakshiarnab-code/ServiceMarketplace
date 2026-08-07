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
  - ServiceRequests, ServiceCategories, ServiceZones, Bids, ServicePackages, ServiceOrders, ServiceOrderMessages
  - SellerProfiles, ProductCategories, ProductInspectionPrompts, ProductListings, ProductDeliveryOrders
  - ServiceOrderPayments, PlatformPaymentIntents, ProviderPayouts, ServiceOrderDisputes, ServiceOrderReviews, ServiceOrderAuditEvents
  - UserNotifications
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

Authorization policies for marketplace workflows are based on capability claims, not administrative roles. Public registration is limited to legacy marketplace account selections: `User`, `ServiceProvider`, and `Both`. `Admin` is reserved for controlled provisioning outside public registration.

```csharp
[Authorize(Policy = "UserOnly")] // requires service.customer capability
public async Task<IActionResult> CreateRequest(...)

[Authorize(Policy = "ProviderOnly")] // requires service.provider capability
public async Task<IActionResult> SubmitBid(...)

[Authorize(Policy = "AdminOnly")] // requires platform.admin administrative permission
public async Task<IActionResult> GetAuditLogs(...)
```

Provider-facing request and bid operations also enforce provider readiness at the service layer. A provider must be active and KYC-approved before browsing available requests or submitting bids. This keeps commercial actions protected even if a controller or policy is accidentally loosened later.

## Database Model

PostgreSQL is the runtime database. EF Core migrations live under `ServiceMarketplace.Infrastructure/Data/Migrations`, and the generated one-shot SQL setup script is `full_migrations.sql`.

Core tables:

| Table | Purpose |
|-------|---------|
| `Users` | Custom account profile, password hash, verification, lockout, and KYC state |
| `Roles` | Seeded marketplace role names: User, ServiceProvider, Both, Admin |
| `UserRoles` | Many-to-many user/role mapping |
| `ServiceRequests` | Customer-created service request records |
| `ServiceCategories` | Active/inactive service catalog categories used by structured requests |
| `ServiceZones` | Coarse service areas exposed to providers before exact address disclosure |
| `Bids` | Provider bids linked to service requests |
| `ServicePackages` | Provider-owned fixed-price packages for bookable service offers |
| `ServiceOrders` | Bid-backed or fixed-package job lifecycle records for customer/provider work |
| `ServiceOrderMessages` | Order-scoped messages between accepted customer/provider participants |
| `ServiceOrderPayments` | One payment record per service order for offline/direct payment bookkeeping |
| `ServiceOrderReviews` | One transaction-backed customer review per completed service order |
| `ServiceOrderAuditEvents` | Append-only transaction history for service order events |
| `UserNotifications` | Persistent notification inbox records for marketplace events |
| `AuditLogs` | Registration, login, logout, session, and admin audit events |
| `RefreshTokens` | Hashed refresh tokens, token family tracking, revocation, and expiry |
| `ServiceProviderProfiles` | Provider onboarding/application profile, service area, verification flags, and review lifecycle |
| `SellerProfiles` | Seller onboarding/application profile, pickup zone, verification flags, and review lifecycle |
| `ProductCategories` | Active/inactive product catalog categories used by listings |
| `ProductInspectionPrompts` | Category-specific used-product inspection prompts shown to buyers and sellers |
| `ProductListings` | Seller-owned product listing records browsable by buyers, including condition disclosure |
| `ProductDeliveryOrders` | Buyer-created product delivery orders with seller fulfillment status, delivery address, and reserved stock accounting |
| `PlatformPaymentIntents` | Server-created platform payment attempts awaiting trusted verification |
| `ProviderPayouts` | Provider payout records created when verified platform payments are released |
| `ServiceOrderDisputes` | Participant-raised disputes for held service-order payments |

Important relationships:

- `ServiceRequests` has many `Bids`.
- `ServiceRequests` has at most one `ServiceOrder`.
- `Bids` has at most one `ServiceOrder` when accepted.
- `ServicePackages` has many `ServiceOrders` when customers book fixed-price packages.
- `SellerProfiles` has many `ProductListings`.
- `ProductListings` references `ProductCategories` and optionally `ServiceZones`.
- `ProductCategories` has many `ProductInspectionPrompts`.
- `ProductListings` has many `ProductDeliveryOrders`.
- `SellerProfiles` has many `ProductDeliveryOrders` through seller user ID.
- `ProductDeliveryOrders` optionally references `ServiceZones`.
- `ServiceOrders` has many `ServiceOrderMessages`.
- `ServiceOrders` has at most one `ServiceOrderPayment`.
- `ServiceOrders` has many `PlatformPaymentIntents` and `ServiceOrderDisputes`.
- `ServiceOrderPayments` can reference one verified `PlatformPaymentIntent`.
- `ServiceOrderPayments` has at most one `ProviderPayout` for released platform funds.
- `ServiceOrders` has at most one `ServiceOrderReview`.
- `ServiceOrders` has many `ServiceOrderAuditEvents`.
- `UserNotifications` stores order, request, bid, and message IDs for inbox deep links.
- `ServiceRequests` optionally references `ServiceCategories` and `ServiceZones`.
- `ServiceProviderProfiles` optionally references `ServiceCategories` and `ServiceZones` for approved provider coverage.
- `Users` has many `UserRoles`; `Roles` has many `UserRoles`.
- `AuditLogs` and `RefreshTokens` store user IDs for auth/session tracking.

## Business Rules

- Accounts with `service.customer` capability can post service requests.
- Accounts with approved `service.provider` capability can browse requests and submit bids.
- Provider accounts also retain default customer capabilities on the same account.
- Admin users can access operational and audit endpoints.
- Provider and Both registrations require age validation for 18+.
- Government ID upload is optional and stored as a file path when submitted.
- Pre-acceptance provider request DTOs do not expose customer IDs, exact addresses, latitude, or longitude.
- Service requests support catalog category, service zone, urgency, preferred start time, and free-form requirements fields.
- Provider request lists prefer normalized service-zone display names over exact customer locations.
- Provider available-request queries are filtered by approved profile availability, service category coverage, and service zone coverage.
- Providers can bid only on open requests that match their approved coverage.
- Customer bid views include provider display/business context, hourly rate, estimated duration, and comparison rank.
- Exact request address and coordinates become visible to the provider only after that provider's bid is accepted.
- Accepting a bid creates a `ServiceOrder` with agreed amount, scheduled start, provider/customer participants, and exact job location.
- Approved providers can create/update fixed-price `ServicePackages` only within their approved category/zone coverage.
- Customers can browse active fixed-price packages and book one directly; booking creates an accepted `ServiceRequest` and a `ServiceOrder` without an accepted bid.
- Package-created orders use the same start, provider-complete, payment, completion, review, notification, and audit workflows as bid-created orders.
- Registered accounts receive default `product.buyer` capability.
- Seller capability is approval-only and emitted as `product.seller` only when a seller profile is approved.
- Approved sellers can create/update product listings; unapproved sellers are blocked by policy and service checks.
- Buyers can browse active product listings with stock by product category, zone, and condition.
- Used product listings require condition notes and seller inspection checklist details before activation.
- Product catalog responses include category-specific used-product inspection prompts.
- Buyers can place product delivery orders against active, stocked listings from approved sellers.
- Product delivery order creation reserves listing stock; cancellation before delivery starts restores stock.
- Sellers advance product delivery orders through pending confirmation, confirmed, ready for pickup, out for delivery, and delivered statuses.
- Direct client-side platform payment recording remains rejected.
- Customers create platform payment intents after provider completion; admin/server verification creates the held platform payment record.
- Customer completion releases held platform payments and creates pending provider payout records.
- Active disputes block completion until admin resolution.
- Admin dispute resolution can refund the customer, release the provider, or reject the dispute.
- Unified marketplace search reads active service packages and active stocked product listings from approved providers/sellers.
- Search supports keyword matching across titles, descriptions, categories, and seller/provider display names.
- Search supports type, service category, product category, zone, product condition, price range, and sort filters.
- Providers can start an order and mark it provider-completed; customers confirm final completion.
- Customers or accepted providers can cancel active orders with a reason.
- Completed or cancelled orders close the original service request.
- Accepted order participants can send messages only within their own order thread.
- Message recipients can mark an order thread read.
- Persistent notifications are emitted for accepted bids, service order creation/start/provider-completion/final-completion/cancellation, and received order messages.
- Customers can record one non-platform payment for the agreed amount after provider completion.
- Platform payment records are rejected until server-side payment verification exists.
- Customer final completion requires a recorded held payment; confirmation releases that recorded payment.
- Completed service orders can receive one customer review with a 1-5 rating and optional feedback.
- Provider profile aggregate rating and review count are updated when a review is created.
- Admins can moderate reviews by hiding/showing them and storing moderation notes.
- Order creation, start, provider completion, payment record, completion release, cancellation, review creation, and review moderation append service order audit events.
- Order DTOs include payment and review summary fields so customer/provider order pages can render transaction state without per-card follow-up calls.
- Customer order UI exposes payment recording as the required step before final completion.
- Provider order UI surfaces payment status and customer review details.
- Customer bid comparison UI surfaces provider rating/review count, hourly rate, estimated duration, comparison rank, and bid message.
- Provider applications follow a persisted lifecycle: Draft, Submitted, Under Review, Approved, More Information Required, Rejected, Suspended, and Revoked.
- Admin review approval synchronizes legacy KYC fields so existing provider gates continue to work during the transition.

## Security Implementation

- JWT access tokens expire after 10 minutes.
- Refresh tokens expire after 7 days and are rotated on refresh.
- Passwords are hashed with PBKDF2 before storage.
- JWT signing keys must be configured through user secrets or environment variables and must be at least 32 bytes.
- Public `Admin` registration is blocked at the API and service layers.
- Marketplace capabilities and administrative permissions are emitted as separate JWT claim types.
- Provider commercial actions require active, approved provider status, preferring the `ServiceProviderProfiles` approval lifecycle with legacy KYC compatibility.
- Product seller actions require approved seller profile status and `product.seller` capability.
- Used product disclosure fields are seller-controlled but API-validated for presence before active used listings can be published.
- Product delivery order APIs are participant-scoped: buyers see their own orders and approved sellers see orders for their listings.
- Sellers cannot create delivery orders for their own product listings.
- Exact request location is withheld from provider listings/details before provider selection and is omitted from JSON when not visible.
- Order message APIs return 404 for non-participants so order existence and thread content are not disclosed.
- Payment and review APIs are order-participant scoped; non-participants receive not-found behavior.
- Platform payment verification, payout paid marking, and dispute resolution are Admin-only.
- Platform payment success is represented only after trusted server/admin verification, not by customer-submitted payment status.
- Unified marketplace search is authenticated and customer-capability scoped so public anonymous marketplace inventory is not exposed yet.
- Client-submitted platform payment success is not accepted; gateway-backed platform payments are deferred.
- Service order audit history is participant-scoped for reads and append-only through service-layer hooks.
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

The test factory uses EF Core in-memory storage and test-only JWT configuration for integration tests. Local development uses PostgreSQL configured through user secrets or environment variables as described in `DATABASE_SETUP.md`.

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
