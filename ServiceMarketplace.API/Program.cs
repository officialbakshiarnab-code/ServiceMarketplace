using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.API.Hubs;
using ServiceMarketplace.API.Configuration;
using ServiceMarketplace.API.Filters;
using ServiceMarketplace.API.Middleware;
using ServiceMarketplace.API.Services;
using ServiceMarketplace.Application.Configuration;
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

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("JWT signing key is not configured. Set Jwt:Key through user secrets or environment variables.");

if (!builder.Environment.IsEnvironment("Testing") &&
    jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("JWT signing key is a placeholder. Configure a real secret outside source control.");
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");

// ==============================
// DATABASE
// ==============================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    options.UseNpgsql(connectionString);
    options.AddInterceptors(new AuditInterceptor());
});

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
            Encoding.UTF8.GetBytes(jwtKey)
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
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrWhiteSpace(accessToken) &&
                path.StartsWithSegments("/hubs/messaging"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
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
                error = context.AuthenticateFailure is SecurityTokenExpiredException ? "access_token_expired" : "unauthorized",
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
    options.AddPolicy("UserOnly", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceCustomer));

    options.AddPolicy("ProviderOnly", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceProvider));

    options.AddPolicy("UserOrBoth", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceCustomer));

    options.AddPolicy("ProviderOrBoth", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceProvider));

    options.AddPolicy("BothRoleOnly", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceCustomer)
            .RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ServiceProvider));

    options.AddPolicy("ProductBuyerOnly", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ProductBuyer));

    options.AddPolicy("ProductSellerOnly", policy =>
        policy.RequireClaim(MarketplaceCapabilityConstants.ClaimType, MarketplaceCapabilityConstants.ProductSeller));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim(AdministrativePermissionConstants.ClaimType, AdministrativePermissionConstants.PlatformAdmin));
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
            partitionKey: RateLimitIdentity.GetPartitionKey(context),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Auth endpoints: Stricter limits to prevent brute force
    options.AddPolicy("auth", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitIdentity.GetPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Token refresh: More lenient than auth but still limited
    options.AddPolicy("refresh", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitIdentity.GetPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Bid placement: Prevent spam bidding
    options.AddPolicy("bids", context =>
    {
        // Extract user ID from JWT if authenticated
        var userId = RateLimitIdentity.GetPartitionKey(context);

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                SegmentsPerWindow = 5,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Request creation: Prevent spam service requests
    options.AddPolicy("requests", context =>
    {
        // Extract user ID from JWT if authenticated
        var userId = RateLimitIdentity.GetPartitionKey(context);

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                SegmentsPerWindow = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Messaging endpoints: keep service-order chat usable while limiting burst abuse.
    options.AddPolicy("messaging", context =>
    {
        var userId = RateLimitIdentity.GetPartitionKey(context);

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Admin endpoints: Higher limits for administrative tasks
    options.AddPolicy("admin", context =>
    {
        var userId = RateLimitIdentity.GetPartitionKey(context);

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
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

        context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter).ToString(System.Globalization.CultureInfo.InvariantCulture);

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

// ==============================
// APPLICATION SERVICES
// ==============================
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<IProviderApplicationService, ProviderApplicationService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();
builder.Services.AddScoped<IServiceOrderCommunicationService, ServiceOrderCommunicationService>();
builder.Services.AddScoped<IServiceOrderPaymentService, ServiceOrderPaymentService>();
builder.Services.AddScoped<IServiceOrderReviewService, ServiceOrderReviewService>();
builder.Services.AddScoped<IServiceOrderAuditService, ServiceOrderAuditService>();
builder.Services.AddScoped<IServicePackageService, ServicePackageService>();
builder.Services.AddScoped<ISellerApplicationService, SellerApplicationService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<IProductListingService, ProductListingService>();
builder.Services.AddScoped<IProductDeliveryOrderService, ProductDeliveryOrderService>();
builder.Services.AddScoped<IMarketplaceEconomicsService, MarketplaceEconomicsService>();
builder.Services.AddScoped<IMarketplaceSearchService, MarketplaceSearchService>();
builder.Services.AddScoped<IProfileDirectoryService, ProfileDirectoryService>();
builder.Services.AddScoped<IContactRequestService, ContactRequestService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageModerationService, MessageModerationService>();
builder.Services.AddScoped<IDeviceRegistrationService, DeviceRegistrationService>();
builder.Services.Configure<PushNotificationSettings>(builder.Configuration.GetSection(PushNotificationSettings.SectionName));
builder.Services.AddHttpClient<FcmPushNotificationSender>();
builder.Services.AddSingleton<NoopPushNotificationSender>();
builder.Services.AddTransient<IPushNotificationSender>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PushNotificationSettings>>().Value;
    return settings.UseFcm
        ? sp.GetRequiredService<FcmPushNotificationSender>()
        : sp.GetRequiredService<NoopPushNotificationSender>();
});
builder.Services.AddSingleton<IConversationRealtimeNotifier, SignalRConversationRealtimeNotifier>();
builder.Services.AddScoped<INotificationService, MarketplaceNotificationService>();
builder.Services.AddScoped<INotificationInboxService, MarketplaceNotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAdminProvisioningService, AdminProvisioningService>();
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
        tags: new[] { "db", "postgres", "ready" })
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
builder.Services.AddSignalR();
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

app.Logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", corsSettings.AllowedOrigins));
app.Logger.LogInformation("HTTPS enforcement: {EnforceHttps}", securitySettings.EnforceHttps);
app.Logger.LogInformation("HSTS enabled: {UseHsts}", securitySettings.UseHsts);

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

// ROLE SEEDING (APPLICATION STARTUP)
using (var scope = app.Services.CreateScope())
{
    var roleSeedingService = scope.ServiceProvider.GetRequiredService<ServiceMarketplace.API.Services.RoleSeedingService>();
    await roleSeedingService.SeedRolesAsync();
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

// Authenticate first so rate limits can use validated user claims.
app.UseAuthentication();

// The dedicated test host opts in without enabling limits for the fast suite.
if (!app.Environment.IsEnvironment("Testing") || builder.Configuration.GetValue<bool>("Testing:EnableRateLimiting"))
{
    app.UseRateLimiter();
}

app.UseAuthorization();
app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging", options =>
{
    options.CloseOnAuthenticationExpiration = true;
});

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

public partial class Program;

