using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.API.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures in-memory database for testing and provides typed HttpClient.
/// </summary>
public class ServiceMarketplaceWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestJwtKey = "TEST_ONLY_JWT_SIGNING_KEY_32_BYTES_MINIMUM_12345";
    private readonly string _databaseName = $"ServiceMarketplaceTest_{Guid.NewGuid():N}";

    public ServiceMarketplaceWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ServiceMarketplace");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ServiceMarketplaceUsers");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Testing");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost");
        Environment.SetEnvironmentVariable("Security__EnforceHttps", "false");
        Environment.SetEnvironmentVariable("Security__UseHsts", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "ServiceMarketplace",
                ["Jwt:Audience"] = "ServiceMarketplaceUsers",
                ["ConnectionStrings:DefaultConnection"] = "Testing",
                ["Cors:AllowedOrigins:0"] = "http://localhost",
                ["Security:EnforceHttps"] = "false",
                ["Security:UseHsts"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }

    public Task<AppDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        return Task.FromResult(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
