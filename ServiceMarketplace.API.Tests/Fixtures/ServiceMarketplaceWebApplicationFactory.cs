using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    private readonly string _databaseName = $"ServiceMarketplaceTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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
