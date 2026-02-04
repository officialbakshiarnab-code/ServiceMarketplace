using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.API.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures in-memory database for testing and provides typed HttpClient.
/// </summary>
public class ServiceMarketplaceWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove production database provider
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            // Add in-memory database for tests
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("ServiceMarketplaceTest");
            });

            // Ensure database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public async Task<AppDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
