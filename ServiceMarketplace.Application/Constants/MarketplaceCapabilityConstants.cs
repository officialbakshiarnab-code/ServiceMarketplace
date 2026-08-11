namespace ServiceMarketplace.Application.Constants;

/// <summary>
/// Marketplace capabilities describe what a normal account can do in the marketplace.
/// They are intentionally separate from administrative permissions.
/// </summary>
public static class MarketplaceCapabilityConstants
{
    public const string ClaimType = "marketplace_capability";

    public const string ProductBuyer = "product.buyer";
    public const string ProductSeller = "product.seller";
    public const string ServiceCustomer = "service.customer";
    public const string ServiceProvider = "service.provider";

    public static IReadOnlyList<string> DefaultRegisteredCapabilities { get; } = new[]
    {
        ProductBuyer,
        ServiceCustomer
    };

    public static IReadOnlyList<string> SellerApprovalCapabilities { get; } = new[]
    {
        ProductSeller
    };

    public static IReadOnlyList<string> ProviderApprovalCapabilities { get; } = new[]
    {
        ServiceProvider
    };

    public static IReadOnlyList<string> FromRoles(IEnumerable<string> roles)
    {
        var normalizedRoles = roles
            .Select(RoleConstants.NormalizeRole)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalizedRoles.Contains(RoleConstants.User) ||
            normalizedRoles.Contains(RoleConstants.ServiceProvider) ||
            normalizedRoles.Contains(RoleConstants.Both))
        {
            foreach (var capability in DefaultRegisteredCapabilities)
                capabilities.Add(capability);
        }

        if (normalizedRoles.Contains(RoleConstants.ServiceProvider) ||
            normalizedRoles.Contains(RoleConstants.Both))
        {
            foreach (var capability in ProviderApprovalCapabilities)
                capabilities.Add(capability);
        }

        return capabilities.ToList();
    }

    public static IReadOnlyList<string> FromPublicRegistrationRole(string role)
    {
        var normalizedRole = RoleConstants.NormalizeRole(role);
        if (normalizedRole == null || !RoleConstants.IsPublicRegistrationRole(normalizedRole))
            return Array.Empty<string>();

        return FromRoles(new[] { normalizedRole });
    }
}
