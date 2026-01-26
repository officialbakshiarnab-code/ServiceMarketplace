namespace ServiceMarketplace.UI.Shared.Auth;

public interface ITokenStorage
{
    Task SaveTokenAsync(string token);
    Task<string?> GetTokenAsync();
    Task ClearAsync();
}
