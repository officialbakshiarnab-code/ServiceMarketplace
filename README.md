# ServiceMarketplace

Service Marketplace is a Clean Architecture solution for connecting **Users** (customers) and **Service Providers** through service requests and bids.

This is a **living document**. Keep it updated whenever the project structure, configuration, or runtime behavior changes.

## ✅ Quick Orientation

This solution contains:

- **ServiceMarketplace.API** — ASP.NET Core Web API with Identity + JWT
- **ServiceMarketplace.Application** — Use cases, DTOs, validators, exceptions
- **ServiceMarketplace.Domain** — Core entities and enums
- **ServiceMarketplace.Infrastructure** — EF Core + Identity + application services
- **ServiceMarketplace.UI.Web** — Blazor WebAssembly app (uses UI.Shared)
- **ServiceMarketplace.UI.MAUI** — MAUI Blazor Hybrid app (uses UI.Shared)
- **ServiceMarketplace.UI.Shared** — Shared Razor components, auth, API clients, DTOs
- **ServiceMarketplace.Shared** — Cross-cutting shared contracts/utilities

For architecture boundaries and dependency rules, see [ARCHITECTURE.md](ARCHITECTURE.md).

## ✅ Configuration (Required)

Set `ApiBaseUrl` for both Web and MAUI UIs:

- [ServiceMarketplace.UI.Web/wwwroot/appsettings.json](ServiceMarketplace.UI.Web/wwwroot/appsettings.json)
- [ServiceMarketplace.UI.MAUI/appsettings.json](ServiceMarketplace.UI.MAUI/appsettings.json)

Example (local dev):

```
"ApiBaseUrl": "https://localhost:7147"
```

API JWT settings are in:

- [ServiceMarketplace.API/appsettings.json](ServiceMarketplace.API/appsettings.json)

## ✅ Build Notes (Linux/CI)

MAUI Android builds require:

- Android SDK installed and `ANDROID_SDK_ROOT` set
- JDK 21 installed and `JAVA_HOME` set

This repository intentionally keeps MAUI iOS/MacCatalyst frameworks gated to macOS builds.

## ✅ Runtime Summary

- API: Swagger available at `https://localhost:7147/swagger`
- Web UI: Blazor WebAssembly app that calls API via `ApiBaseUrl`
- MAUI UI: Hybrid app using shared components + shared API client

## ✅ Documentation Maintenance

Keep updating this README and [ARCHITECTURE.md](ARCHITECTURE.md) whenever:

- New projects are added
- New API endpoints or auth flows are added
- Dependency rules or deployment steps change