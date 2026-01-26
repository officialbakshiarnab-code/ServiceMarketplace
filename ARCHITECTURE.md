# Architecture Guide (Clean Architecture)

This document describes **project boundaries**, **dependency rules**, and **responsibilities** in the Service Marketplace solution.

This is a **living document** — update it whenever architecture, layering, or cross-cutting behaviors change.

---

## 1) Layered Boundaries

**Dependency Rule:**
- `Domain` has **no dependencies** on other projects.
- `Application` depends only on `Domain`.
- `Infrastructure` depends on `Application` and `Domain`.
- `API` depends on `Application` + `Infrastructure`.
- `UI.*` consumes API via HTTP and uses `UI.Shared` for shared UI/auth/DTOs.

```
Domain
↑
Application
↑
Infrastructure
↑
API
↑
UI (Web/MAUI) → UI.Shared
```

---

## 2) Project Responsibilities

### ServiceMarketplace.Domain
- Core business entities
- Domain enums
- No infrastructure or HTTP concerns

### ServiceMarketplace.Application
- DTOs (input/output)
- Interfaces for services
- Validation rules
- Custom exceptions

### ServiceMarketplace.Infrastructure
- EF Core context
- Identity storage integration
- Concrete service implementations

### ServiceMarketplace.API
- HTTP endpoints (Controllers)
- Authentication + authorization
- Exception handling and validation middleware

### ServiceMarketplace.UI.Shared
- Shared Razor components
- Auth logic + API clients
- Shared UI DTOs and auth helpers

### ServiceMarketplace.UI.Web
- Blazor WebAssembly host
- Consumes `UI.Shared`

### ServiceMarketplace.UI.MAUI
- MAUI Blazor Hybrid host
- Consumes `UI.Shared`

### ServiceMarketplace.Shared
- Cross-cutting shared contracts/utilities

---

## 3) Cross-Cutting Concerns

- **Validation**: FluentValidation in API layer.
- **Error handling**: Centralized exception middleware returns ProblemDetails.
- **Auth**: JWT bearer token with role-based policies (User/ServiceProvider).

---

## 4) Configuration

- API config lives in [ServiceMarketplace.API/appsettings.json](ServiceMarketplace.API/appsettings.json)
- UI config uses ApiBaseUrl:
  - [ServiceMarketplace.UI.Web/wwwroot/appsettings.json](ServiceMarketplace.UI.Web/wwwroot/appsettings.json)
  - [ServiceMarketplace.UI.MAUI/appsettings.json](ServiceMarketplace.UI.MAUI/appsettings.json)

---

## 5) Update Expectations

Update this document when:
- Architecture or dependency rules change
- A new project/layer is introduced
- Core auth or error-handling patterns change
- Shared UI/auth services are refactored
