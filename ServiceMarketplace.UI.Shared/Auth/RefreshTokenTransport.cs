namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>Dedicated transport without the authorization handler: refresh cannot recurse.</summary>
public sealed class RefreshTokenTransport(HttpClient client) : IDisposable
{
    public HttpClient Client { get; } = client;
    public void Dispose() => Client.Dispose();
}
