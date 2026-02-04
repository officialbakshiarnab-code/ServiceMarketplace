using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.API.Configuration;
using ServiceMarketplace.API.Filters;
using ServiceMarketplace.API.Middleware;
using ServiceMarketplace.API.Services;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Application.Validators;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Services;
using System.Text;
using System.Threading.RateLimiting;

// Purpose: Web API host for Service Marketplace.
// - Controllers: HTTP endpoints (Auth, Requests, Bids)
// - Filters: cross-cutting validation
// - Middleware: consistent error handling
// - Models: API request/response contracts
// - Infrastructure: data + services wired via DI

var builder = WebApplication.CreateBuilder(args);

// ==============================
// SECURITY CONFIGURATION
// ==============================
var corsSettings = builder.Configuration
    .GetSection(CorsSettings.SectionName)
    .Get<CorsSettings>() ?? new CorsSettings();

corsSettings.Validate();

var securitySettings = builder.Configuration
    .GetSection(SecuritySettings.SectionName)
    .Get<SecuritySettings>() ?? new SecuritySettings();

builder.Logging.AddConsole();
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", corsSettings.AllowedOrigins));
logger.LogInformation("HTTPS enforcement: {EnforceHttps}", securitySettings.EnforceHttps);
logger.LogInformation("HSTS enabled: {UseHsts}", securitySettings.UseHsts);

// ==============================
// DATABASE
// ==============================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(new AuditInterceptor());
});

// ==============================
// IDENTITY
// ==============================
builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ==============================
// JWT AUTHENTICATION
// ==============================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
        )
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = async context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var tokenValue = !string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader["Bearer ".Length..].Trim()
                    : null;

                using var scope = context.HttpContext.RequestServices.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                await authService.HandleTokenExpiredAsync(
                    tokenValue,
                    context.Request.Headers.UserAgent.ToString(),
                    context.HttpContext.Connection.RemoteIpAddress?.ToString());
            }
        },
        OnChallenge = async context =>
        {
            if (context.Handled)
                return;

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "unauthorized",
                message = "Authentication is required to access this resource.",
                traceId = context.HttpContext.TraceIdentifier
            });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "forbidden",
                message = "You are not allowed to access this resource.",
                traceId = context.HttpContext.TraceIdentifier
            });
        }
    };
});

// ==============================
// AUTHORIZATION
// ==============================
builder.Services.AddAuthorization(options =>
{
    // Default require User role - can create requests, accept bids
    options.AddPolicy("UserOnly", policy =>
        policy.RequireRole(RoleConstants.User));

    // Default require ServiceProvider role - can browse, bid
    options.AddPolicy("ProviderOnly", policy =>
        policy.RequireRole(RoleConstants.ServiceProvider));

    // Users with Both role can access both User and Provider features
    options.AddPolicy("UserOrBoth", policy =>
        policy.RequireRole(RoleConstants.User, RoleConstants.Both));

    // Providers and Both users can bid
    options.AddPolicy("ProviderOrBoth", policy =>
        policy.RequireRole(RoleConstants.ServiceProvider, RoleConstants.Both));

    // Dual-role users (Both) have full access
    options.AddPolicy("BothRoleOnly", policy =>
        policy.RequireRole(RoleConstants.Both));
});

// ==============================
// RATE LIMITING
// ==============================
builder.Services.AddRateLimiter(options =>
{
    // Default policy: Sliding window for general endpoints
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100, // 100 requests
                Window = TimeSpan.FromMinutes(1), // per minute
                SegmentsPerWindow = 6, // divided into 6 segments (10 seconds each)
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0 // No queuing, reject immediately
            });
    });

    // Auth endpoints: Stricter limits to prevent brute force
    options.AddPolicy("auth", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, // 5 attempts
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Token refresh: More lenient than auth but still limited
    options.AddPolicy("refresh", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10, // 10 refreshes
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Bid placement: Prevent spam bidding
    options.AddPolicy("bids", context =>
    {
        // Extract user ID from JWT if authenticated
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10, // 10 bids
                Window = TimeSpan.FromMinutes(5), // per 5 minutes
                SegmentsPerWindow = 5, // 1 minute segments
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Request creation: Prevent spam service requests
    options.AddPolicy("requests", context =>
    {
        // Extract user ID from JWT if authenticated
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5, // 5 requests
                Window = TimeSpan.FromMinutes(10), // per 10 minutes
                SegmentsPerWindow = 10, // 1 minute segments
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Admin endpoints: Higher limits for administrative tasks
    options.AddPolicy("admin", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 200, // 200 requests
                Window = TimeSpan.FromMinutes(1), // per minute
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Customize rejection response
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
            ? retryAfterValue.TotalSeconds
            : 60;

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "rate_limit_exceeded",
            message = "Too many requests. Please try again later.",
            retryAfter = (int)retryAfter,
            traceId = context.HttpContext.TraceIdentifier
        }, cancellationToken: cancellationToken);
    };
});

// ==============================
// CORS (WHY: Blazor WASM runs on different origin than API; browser blocks requests without CORS policy)
// Production-hardened: Only specific origins allowed, no wildcards
// ==============================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsSettings.AllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
        
        // Log CORS configuration for security audit
        foreach (var origin in corsSettings.AllowedOrigins)
        {
            logger.LogInformation("CORS origin allowed: {Origin}", origin);
        }
    });
});

// ==============================
// HTTPS & COOKIE SECURITY
// ==============================
if (securitySettings.EnforceHttps)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 7147;
    });
}

if (securitySettings.UseHsts)
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromSeconds(securitySettings.HstsMaxAgeSeconds);
        options.IncludeSubDomains = true;
        options.Preload = true;
    });
}

// Configure secure cookie policy
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;  // Lax for OAuth/external auth compatibility
    options.Secure = securitySettings.EnforceHttps 
        ? CookieSecurePolicy.Always 
        : CookieSecurePolicy.SameAsRequest;
});

// Configure ASP.NET Identity cookie security
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = securitySettings.EnforceHttps 
        ? CookieSecurePolicy.Always 
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ==============================
// APPLICATION SERVICES
// ==============================
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<INotificationService, EmailNotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();
builder.Services.AddScoped<IAdminKpiService, AdminKpiService>();
builder.Services.AddScoped<ICleanupService, CleanupService>();
builder.Services.AddScoped<ServiceMarketplace.Infrastructure.Services.IFileUploadService, FileUploadService>();
builder.Services.AddScoped<ServiceMarketplace.API.Services.RoleSeedingService>();

// ==============================
// BACKGROUND SERVICES
// ==============================
builder.Services.AddHostedService<ServiceMarketplace.API.BackgroundServices.RefreshTokenCleanupService>();
builder.Services.AddHostedService<ServiceMarketplace.API.BackgroundServices.AbandonedSessionCleanupService>();
builder.Services.AddHostedService<ServiceMarketplace.API.BackgroundServices.AuditLogArchivalService>();

// ==============================
// HEALTH CHECKS
// ==============================
builder.Services.AddHealthChecks()
    .AddCheck<ServiceMarketplace.API.HealthChecks.DatabaseHealthCheck>(
        "database",
        tags: new[] { "db", "sql", "ready" })
    .AddCheck<ServiceMarketplace.API.HealthChecks.AuthSubsystemHealthCheck>(
        "auth_subsystem",
        tags: new[] { "auth", "identity", "ready" })
    .AddCheck<ServiceMarketplace.API.HealthChecks.BackgroundJobsHealthCheck>(
        "background_jobs",
        tags: new[] { "jobs", "background", "ready" });
// ==============================
// CONTROLLERS + SWAGGER
// ==============================
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidateModelFilter>();
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateServiceRequestDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateBidDtoValidator>();

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ==============================
// DATABASE MIGRATION (DEVELOPMENT ONLY)
// ==============================
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            dbContext.Database.Migrate();
            app.Logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Error applying database migrations");
            throw;
        }
    }
}

// ==============================
// ROLE SEEDING (APPLICATION STARTUP)
// ==============================
// Seed all required roles (User, ServiceProvider, Admin) once during startup
// This ensures roles exist before any registration attempts
using (var scope = app.Services.CreateScope())
{
    var roleSeedingService = scope.ServiceProvider.GetRequiredService<ServiceMarketplace.API.Services.RoleSeedingService>();
    try
    {
        await roleSeedingService.SeedRolesAsync();
        app.Logger.LogInformation("Roles seeded successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error seeding roles during application startup");
        throw;
    }
}

// ==============================
// MIDDLEWARE PIPELINE
// ==============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Production: Use HSTS for security
    if (securitySettings.UseHsts)
    {
        app.UseHsts();
    }
}

// Apply security headers to all responses
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Apply cookie policy before other middleware
app.UseCookiePolicy();

// Force HTTPS redirection in production
if (securitySettings.EnforceHttps)
{
    app.UseHttpsRedirection();
}

// WHY: CORS must be applied before Authentication/Authorization/Controllers
app.UseCors();

// Apply rate limiting before authentication
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ==============================
// HEALTH CHECK ENDPOINTS
// ==============================
// Detailed health check with all information
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = ServiceMarketplace.API.HealthChecks.HealthCheckResponseWriter.WriteResponse,
    AllowCachingResponses = false
});

// Readiness probe (checks if app is ready to accept traffic)
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = ServiceMarketplace.API.HealthChecks.HealthCheckResponseWriter.WriteResponse,
    AllowCachingResponses = false
});

// Liveness probe (checks if app is alive and should stay running)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // No checks, just returns 200 OK if app is running
    AllowCachingResponses = false
});

app.Run();

