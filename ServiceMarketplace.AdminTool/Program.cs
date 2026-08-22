using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Services;
using System.Text;

var options = AdminToolOptions.Parse(args);
if (!options.IsValid)
{
    Console.Error.WriteLine(options.Error);
    Console.Error.WriteLine(AdminToolOptions.Usage);
    return 2;
}

var password = Environment.GetEnvironmentVariable("SERVICE_MARKETPLACE_ADMIN_PASSWORD");
if (string.IsNullOrWhiteSpace(password))
    password = ReadPassword("Admin password: ");

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
}));
services.AddDbContext<AppDbContext>(db => db.UseNpgsql(options.ConnectionString));
services.AddScoped<IPasswordHasher, PasswordHasher>();
services.AddScoped<IAuditLogService, AuditLogService>();
services.AddScoped<IAdminProvisioningService, AdminProvisioningService>();

await using var provider = services.BuildServiceProvider();
var provisioningService = provider.GetRequiredService<IAdminProvisioningService>();

var result = await provisioningService.ProvisionAsync(new AdminProvisioningRequest
{
    Email = options.Email!,
    FirstName = options.FirstName!,
    LastName = options.LastName!,
    PhoneNumber = options.Phone!,
    Password = password,
    Reason = options.Reason!,
    ResetPassword = options.ResetPassword,
    OperatorUserId = options.OperatorUserId
});

if (!result.Success)
{
    Console.Error.WriteLine($"Admin bootstrap failed: {result.Message}");
    return 1;
}

Console.WriteLine(result.Message);
Console.WriteLine($"Admin user id: {result.UserId}");
Console.WriteLine($"Created user: {result.CreatedUser}");
Console.WriteLine($"Granted admin role: {result.GrantedAdminRole}");
Console.WriteLine($"Password reset: {result.PasswordReset}");
return 0;

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var builder = new StringBuilder();

    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (builder.Length > 0)
            {
                builder.Length--;
                Console.Write("\b \b");
            }
            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            builder.Append(key.KeyChar);
            Console.Write('*');
        }
    }

    return builder.ToString();
}

internal sealed class AdminToolOptions
{
    public string? ConnectionString { get; private init; }
    public string? Email { get; private init; }
    public string? FirstName { get; private init; }
    public string? LastName { get; private init; }
    public string? Phone { get; private init; }
    public string? Reason { get; private init; }
    public string? OperatorUserId { get; private init; }
    public bool ResetPassword { get; private init; }
    public bool IsValid { get; private init; }
    public string? Error { get; private init; }

    public const string Usage = """
Usage:
  dotnet run --project ServiceMarketplace.AdminTool -- \
    --connection "<connection-string>" \
    --email admin@test.com \
    --first-name Admin \
    --last-name User \
    --phone 9999999999 \
    --reason "Local pilot bootstrap" \
    [--reset-password]

Password:
  Set SERVICE_MARKETPLACE_ADMIN_PASSWORD or enter it at the secure prompt.
""";

    public static AdminToolOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resetPassword = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--reset-password", StringComparison.OrdinalIgnoreCase))
            {
                resetPassword = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
                return Invalid($"Unexpected argument '{arg}'.");

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                return Invalid($"Missing value for '{arg}'.");

            values[arg[2..]] = args[++i];
        }

        var connection = Get(values, "connection") ??
            Get(values, "connection-string") ??
            Environment.GetEnvironmentVariable("SERVICE_MARKETPLACE_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        var options = new AdminToolOptions
        {
            ConnectionString = connection,
            Email = Get(values, "email"),
            FirstName = Get(values, "first-name"),
            LastName = Get(values, "last-name"),
            Phone = Get(values, "phone"),
            Reason = Get(values, "reason"),
            OperatorUserId = Get(values, "operator-user-id"),
            ResetPassword = resetPassword
        };

        var missing = new[]
            {
                (Name: "connection", Value: options.ConnectionString),
                (Name: "email", Value: options.Email),
                (Name: "first-name", Value: options.FirstName),
                (Name: "last-name", Value: options.LastName),
                (Name: "phone", Value: options.Phone),
                (Name: "reason", Value: options.Reason)
            }
            .Where(item => string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Name)
            .ToList();

        return missing.Count > 0
            ? Invalid($"Missing required option(s): {string.Join(", ", missing)}.")
            : new AdminToolOptions
            {
                ConnectionString = options.ConnectionString,
                Email = options.Email,
                FirstName = options.FirstName,
                LastName = options.LastName,
                Phone = options.Phone,
                Reason = options.Reason,
                OperatorUserId = options.OperatorUserId,
                ResetPassword = options.ResetPassword,
                IsValid = true
            };
    }

    private static string? Get(Dictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) ? value : null;
    }

    private static AdminToolOptions Invalid(string message)
    {
        return new AdminToolOptions { IsValid = false, Error = message };
    }
}
