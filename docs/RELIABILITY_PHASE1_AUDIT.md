# Reliability hardening phase 1: pre-edit audit (2026-09-14)

Inspected the working tree, including existing uncommitted registration and host changes. No implementation changes preceded this audit. Registration V1.1 remains frozen except a demonstrated regression fix.

| Finding | Classification | Evidence before changes |
| --- | --- | --- |
| Automatic refresh wiring | CONFIRMED | Both hosts supply ordinary HttpClient with attach-only AuthorizingHttpClientHandler. The Web-only TokenRefreshHttpClient is registered but not consumed by feature clients. AuthenticationStateProvider additionally clears tokens at expiry. |
| Rate-limit ordering | CONFIRMED | UseRateLimiter precedes UseAuthentication; named user policies inspect HttpContext.User. Global/auth/refresh policies use remote IP. |
| Multi-save integrity and side effects | PARTIALLY CONFIRMED | Single SaveChanges is already atomic; registration has an explicit relational transaction. Order workflows save core state before audit/conversation/notifications. SignalR can throw after message persistence; FCM delivery exceptions are mostly caught, but surrounding database work can still fail. |
| Product stock concurrency | CONFIRMED | Read/check/decrement has no concurrency token, conditional update or lock. Cancellation checks/restoration also use unprotected reads. Seller listing edits write stock too. |
| Test database fidelity | CONFIRMED | Main factory replaces Npgsql with InMemory. No relational test layer. Two rate-limit tests are skipped because Testing disables middleware; several remaining rate tests have vacuous assertions. |
| Platform payment verification | INTENTIONAL DESIGN | MarketplaceEconomicsService records an administrator-supplied payment reference; no external gateway verification. Held/Released/Refunded are ledger/workflow statuses. |
| Browser token storage | INTENTIONAL DESIGN | WebSessionTokenStorage uses fields; startup removes legacy browser-storage values. Reload signs out. MAUI uses SecureStorage. |

## Scope traced

API composition/JWT validation/policies/pipeline; client handlers, storage, AuthApiClient, feature clients and authentication-state expiry; server AuthService, TokenRefreshService and RefreshTokenEntity; requests/bids/orders/packages/payments/economics/products; conversations/legacy messaging/notifications/audits; AppDbContext and entity mappings; test factory and tests; PostgreSQL factory/settings; local setup/start scripts; central ProjectBrain work state, architecture, QA plan, log and backlog.

## Workflow boundaries before changes

- Bid acceptance: selected/rejected bids + request + order in one save; critical audit and conversation in later saves; notifications later.
- Package booking: request + order in one save; audit/conversation/notifications later.
- Start/provider completion/cancellation: core order saved before audit/system messages/notifications.
- Customer completion: order/request/payment saved; payout saved separately; audit/system messages/notifications later.
- Direct payment and platform verification: payment/core saved before audit and notifications.
- Dispute creation/resolution: core saved before audit and notifications; resolution can create payout.
- Product creation/cancellation: stock and order share one save but reads are not concurrency protected; notification save follows.
- Conversation creation: conversation/participants saved before initial system message; SignalR follows persistence.
- HTTP messages: message persisted before SignalR; legacy HTTP messages saved before mirrored conversation messages.
- Audit history: durable business records must not be treated as best-effort logging.
- Notifications: MarketplaceNotificationService writes relational inbox rows; external SignalR/FCM are separate calls.

## Baseline evidence

- `dotnet build ServiceMarketplace.sln --no-restore`: 0 warnings, 0 errors.
- Full API tests: 169 passed, 1 failed, 2 skipped (172 total).
- Existing failure: RegistrationBotConversationTests.ProviderSellerFlow_MapsToExistingCommercialOnboardingPayload expects Home Appliance Repair; mapped category is empty. Investigate under permitted regression exception.
- Local Windows PostgreSQL 18 service is running. Prefer opt-in tests against isolated disposable databases on existing PostgreSQL; no Docker requirement.

## Decisions to implement

- Shared session-scoped refresh synchronization, dedicated non-recursive refresh transport, one replay only on an explicit expired-token response. Preserve token storage and server security contracts.
- Authenticate before partitioning; trust authenticated NameIdentifier only, otherwise server remote IP. Enable middleware in a dedicated test host.
- Small nested-aware relational workflow boundary for existing multi-save operations; queue external publication until commit and log post-commit failures. Keep required audit/conversation/inbox persistence atomic. No outbox or network inside transactions.
- PostgreSQL row locks for stock reservation/restoration and delivery transitions; include seller stock edits in the locking policy. No schema redesign.
- Opt-in PostgreSQL tests for concurrency, rollback, constraints and representative queries; keep fast tests independent.

## Reference semantics

- https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0
- https://www.postgresql.org/docs/18/explicit-locking.html

Remaining cross-process refresh rotation atomicity and cancellation/login races will be checked during implementation; preserve reuse rejection rather than invent idempotent replay of secret tokens.
