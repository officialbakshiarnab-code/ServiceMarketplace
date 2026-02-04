# ServiceMarketplace

A modern, full-stack service marketplace platform that connects **customers** seeking services with **providers** offering specialized expertise. Built with .NET 9, Clean Architecture, and cloud-ready patterns.

## 🎯 Features

- **User Account Types**
  - **Customer**: Post service requests, review bids, hire providers
  - **Service Provider**: Browse open requests, submit bids, build reputation
  - **Both**: Operate as customer and provider simultaneously
  - **Admin**: Manage platform, audit activity, enforce policies

- **Service Request Lifecycle**
  - Post detailed service requests with location and category
  - Receive competitive bids from qualified providers
  - Compare bids and select the best match
  - Integrated messaging and status tracking

- **Authentication & Security**
  - JWT-based authentication with refresh tokens
  - Automatic token rotation and revocation tracking
  - Role-based access control (RBAC)
  - Complete audit trail of all auth events
  - Age validation for service providers (18+)

- **Core Capabilities**
  - Real-time bid notifications
  - Provider rating system
  - Geolocation-based provider discovery
  - Government ID verification option
  - Comprehensive audit logging
  - Rate limiting for API protection

## 🏗️ Architecture

Built on **Clean Architecture** principles with clear separation of concerns:

| Layer | Responsibility |
|-------|---|
| **API** | ASP.NET Core WebAPI, controllers, middleware |
| **Application** | Business use cases, DTOs, validators, exceptions |
| **Domain** | Core entities, enums, value objects |
| **Infrastructure** | Data access, external services, background jobs |
| **UI** | Blazor WebAssembly (Web) and MAUI Hybrid (Mobile) |

## 💻 Tech Stack

| Component | Technology |
|-----------|---|
| **Backend API** | ASP.NET Core 9.0 |
| **Database** | SQL Server 2019+ |
| **Authentication** | ASP.NET Core Identity + JWT |
| **Web UI** | Blazor WebAssembly (.NET 9) |
| **Mobile UI** | .NET MAUI Hybrid |
| **Shared Components** | Razor Components (Web & Mobile) |
| **Architecture** | Clean Architecture + SOLID |

## 🚀 Getting Started

### Prerequisites

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server 2019+** - Local instance or cloud database
- **Visual Studio 2022+** or VS Code with C# extensions

### Configuration

1. **API Configuration** - `ServiceMarketplace.API/appsettings.json`
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.;Database=ServiceMarketplaceDB;Trusted_Connection=true;"
     },
     "Jwt": {
       "Key": "your-256-bit-secret-key-here",
       "Issuer": "ServiceMarketplace",
       "Audience": "ServiceMarketplaceUsers"
     }
   }
   ```

2. **Web UI Configuration** - `ServiceMarketplace.UI.Web/wwwroot/appsettings.json`
   ```json
   {
     "ApiBaseUrl": "https://localhost:7147"
   }
   ```

3. **MAUI Configuration** - `ServiceMarketplace.UI.MAUI/appsettings.json`
   ```json
   {
     "ApiBaseUrl": "https://your-production-api.com"
   }
   ```

### Run Locally

```bash
# Clone and navigate to repository
git clone https://github.com/officialbakshiarnab-code/ServiceMarketplace.git
cd ServiceMarketplace

# Restore dependencies
dotnet restore

# Apply database migrations
dotnet ef database update --project ServiceMarketplace.Infrastructure

# Start the API (runs on https://localhost:7147)
dotnet run --project ServiceMarketplace.API

# In another terminal, start the Web UI (runs on https://localhost:7241)
dotnet run --project ServiceMarketplace.UI.Web

# API documentation available at https://localhost:7147/swagger
```

## 🔐 Authentication

The platform uses **JWT tokens** for stateless authentication:

- **Access Token**: 10-minute lifetime, included in API requests
- **Refresh Token**: 7-day lifetime, automatically rotated for security
- **SessionId**: Unique identifier for audit trail tracking

Login flow:
1. User submits email + password
2. Server validates and issues JWT + Refresh Token
3. Client stores tokens securely
4. API calls include JWT in Authorization header
5. When expired, client uses Refresh Token to get new access token

## 👥 Role-Based Access

| Role | Capabilities |
|------|---|
| **Customer** | Create requests, accept bids, leave feedback |
| **Provider** | Browse requests, submit bids, build profile |
| **Both** | Both customer and provider capabilities |
| **Admin** | Manage users, audit logs, system settings |

## 📊 Project Structure

```
ServiceMarketplace/
├── ServiceMarketplace.API/           # Web API
├── ServiceMarketplace.Application/   # Business logic
├── ServiceMarketplace.Domain/        # Core entities
├── ServiceMarketplace.Infrastructure/# Data access
├── ServiceMarketplace.UI.Web/        # Blazor WASM
├── ServiceMarketplace.UI.MAUI/       # .NET MAUI
├── ServiceMarketplace.UI.Shared/     # Shared components
└── ServiceMarketplace.Shared/        # Utilities
```

## 🧪 Testing

The solution includes integration tests covering:
- User registration and login flows
- Role-based authorization enforcement
- Token refresh and expiration handling
- Bid submission and acceptance workflows

Run tests:
```bash
dotnet test
```

## 📈 Production Deployment

For production deployments:
- Set `Jwt:Key` to a strong 256-bit secret
- Update CORS origins to production domains
- Enable HTTPS with valid certificates
- Configure SQL Server with appropriate backups
- Enable rate limiting and security headers
- Review audit logs regularly

See [TECHNICAL-README.md](TECHNICAL-README.md) for detailed deployment guidance.

## 🤝 Contributing

1. Create a feature branch (`git checkout -b feature/your-feature`)
2. Make your changes with clear commit messages
3. Ensure tests pass (`dotnet test`)
4. Submit a pull request

## 📄 License

This project is proprietary. All rights reserved.

## 📞 Support

For issues or questions:
- Review [TECHNICAL-README.md](TECHNICAL-README.md) for architecture details
- Check API documentation at `/swagger` endpoint
- Review code comments for implementation details

---

**Last Updated**: February 2025 | **Status**: Production Ready