using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.API.Filters;
using ServiceMarketplace.API.Middleware;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Application.Validators;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Services;
using System.Text;

// Purpose: Web API host for Service Marketplace.
// - Controllers: HTTP endpoints (Auth, Requests, Bids)
// - Filters: cross-cutting validation
// - Middleware: consistent error handling
// - Models: API request/response contracts
// - Infrastructure: data + services wired via DI

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
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
builder.Services.AddAuthorization();

// ==============================
// CORS (WHY: Blazor WASM runs on different origin than API; browser blocks requests without CORS policy)
// ==============================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "https://localhost:7241",  // UI.Web HTTPS
            "http://localhost:5241"     // UI.Web HTTP
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// ==============================
// APPLICATION SERVICES
// ==============================
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<INotificationService, EmailNotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuthService, AuthService>();

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
// MIDDLEWARE PIPELINE
// ==============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

// WHY: CORS must be applied before Authentication/Authorization/Controllers
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
