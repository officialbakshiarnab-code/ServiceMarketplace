using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class BidsApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task PlaceBidAsync(CreateBidDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.PostAsJsonAsync("api/bids", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Bid failed.");
    }

    public async Task<List<BidDto>> GetBidsForRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync($"api/bids/{requestId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load bids.");

        return await response.Content.ReadFromJsonAsync<List<BidDto>>(cancellationToken: cancellationToken) ?? [];
    }

    private async Task AttachBearerAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var token = await _tokenStorage.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
