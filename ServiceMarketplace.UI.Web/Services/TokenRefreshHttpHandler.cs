namespace ServiceMarketplace.UI.Web.Services;

/// <summary>Compatibility wrapper; the shared authorization handler now owns refresh and replay.</summary>
public sealed class TokenRefreshHttpClient(HttpClient httpClient)
{
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        => httpClient.SendAsync(request, cancellationToken);
}
