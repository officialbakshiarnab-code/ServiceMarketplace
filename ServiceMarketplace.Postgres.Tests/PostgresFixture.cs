using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Services;
using Xunit;

namespace ServiceMarketplace.Postgres.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SERVICEMARKETPLACE_TEST_POSTGRES")))
            Skip = "Opt-in PostgreSQL test: set SERVICEMARKETPLACE_TEST_POSTGRES or run scripts/test-postgres.ps1 -UseLocalDevelopmentConnection.";
    }
}

/// <summary>Real migrations in a unique schema; never creates/drops the configured database or public schema.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _schema = "sm_reliability_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private bool _schemaCreated;
    public ServiceProvider Services { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("SERVICEMARKETPLACE_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;
        var connection = new NpgsqlConnectionStringBuilder(_adminConnection);
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("These tests only accept a local PostgreSQL server.");
        connection.IncludeErrorDetail = false;
        connection.SearchPath = _schema;
        ConnectionString = connection.ConnectionString;
        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", admin);
            await create.ExecuteNonQueryAsync();
            _schemaCreated = true;
        }
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "RELATIONAL_TEST_SIGNING_KEY_32_BYTES_MINIMUM_12345",
                ["Jwt:Issuer"] = "ServiceMarketplace", ["Jwt:Audience"] = "ServiceMarketplaceUsers"
            }).Build());
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", _schema)).AddInterceptors(new AuditInterceptor()));
            services.AddScoped<INotificationService, MarketplaceNotificationService>();
            services.AddScoped<IServiceOrderAuditService, ServiceOrderAuditService>();
            services.AddScoped<IConversationService, ConversationService>();
            services.AddScoped<IConversationRealtimeNotifier, NoopConversationRealtimeNotifier>();
            services.AddScoped<IPushNotificationSender, NoopPushNotificationSender>();
            services.AddScoped<IServiceOrderService, ServiceOrderService>();
            services.AddScoped<IServiceRequestService, ServiceRequestService>();
            services.AddScoped<IServiceOrderPaymentService, ServiceOrderPaymentService>();
            services.AddScoped<IMarketplaceEconomicsService, MarketplaceEconomicsService>();
            services.AddScoped<IProductDeliveryOrderService, ProductDeliveryOrderService>();
            services.AddScoped<IProductListingService, ProductListingService>();
            services.AddScoped<ITokenRefreshService, TokenRefreshService>();
            Services = services.BuildServiceProvider();
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }
        catch { await DisposeAsync(); throw; }
    }

    public async Task DisposeAsync()
    {
        if (Services != null) await Services.DisposeAsync();
        if (!_schemaCreated || string.IsNullOrWhiteSpace(_adminConnection)) return;
        // Generated identifier, checked before the only destructive operation.
        if (!System.Text.RegularExpressions.Regex.IsMatch(_schema, "^sm_reliability_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Unsafe test schema identifier.");
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", admin);
        await drop.ExecuteNonQueryAsync();
        _schemaCreated = false;
    }
}
