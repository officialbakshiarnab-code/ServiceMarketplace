using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>One refresh at a time per client session, shared by HTTP and UI expiry handling.</summary>
public sealed class SessionTokenRefreshService(
    ITokenStorage storage, RefreshTokenTransport transport, ILogger<SessionTokenRefreshService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _generation;
    public long Generation => Interlocked.Read(ref _generation);
    public event Action? Changed;
    public Uri ApiBaseUri => transport.Client.BaseAddress!;

    public async Task<bool> RefreshAsync(string expiredAccessToken, CancellationToken cancellationToken = default, long? expectedGeneration = null)
    {
        var generation = expectedGeneration ?? Generation;
        await _gate.WaitAsync(cancellationToken);
        var changed = false;
        try
        {
            var current = await storage.GetTokenAsync();
            if (generation != Interlocked.Read(ref _generation) || string.IsNullOrWhiteSpace(current)) return false;
            // Another request already rotated the pair while this request waited.
            if (!string.Equals(current, expiredAccessToken, StringComparison.Ordinal)) return true;
            var refreshToken = await storage.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                await storage.ClearAsync();
                changed = true;
                return false;
            }
            try
            {
                // Once rotation starts it must finish independently of caller cancellation.
                // The dedicated transport has a bounded timeout.
                using var response = await transport.Client.PostAsJsonAsync(
                    "api/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken });
                var pair = response.IsSuccessStatusCode
                    ? await response.Content.ReadFromJsonAsync<RefreshTokenResponse>() : null;
                if (generation != Interlocked.Read(ref _generation)) return false;
                if (pair == null || string.IsNullOrWhiteSpace(pair.AccessToken) ||
                    string.IsNullOrWhiteSpace(pair.RefreshToken) || pair.AccessTokenExpiresAt <= DateTime.UtcNow)
                {
                    logger.LogWarning("Session refresh rejected. StatusCode: {StatusCode}", (int)response.StatusCode);
                    await storage.ClearAsync();
                    changed = true;
                    return false;
                }
                await storage.SaveRefreshTokenAsync(pair.RefreshToken);
                await storage.SaveTokenAsync(pair.AccessToken);
                changed = true;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Session refresh failed. FailureType: {FailureType}", ex.GetType().Name);
                if (generation == Interlocked.Read(ref _generation))
                {
                    await storage.ClearAsync();
                    changed = true;
                }
                return false;
            }
        }
        finally
        {
            _gate.Release();
            if (changed) NotifyChanged();
        }
    }

    public async Task SetTokensAsync(string accessToken, string? refreshToken)
    {
        Interlocked.Increment(ref _generation);
        await _gate.WaitAsync();
        try
        {
            await storage.ClearAsync();
            if (!string.IsNullOrWhiteSpace(refreshToken)) await storage.SaveRefreshTokenAsync(refreshToken);
            await storage.SaveTokenAsync(accessToken);
        }
        finally { _gate.Release(); }
        NotifyChanged();
    }

    public async Task SignOutAsync(long? expectedGeneration = null)
    {
        if (expectedGeneration.HasValue)
        {
            if (Interlocked.CompareExchange(ref _generation, expectedGeneration.Value + 1, expectedGeneration.Value) != expectedGeneration.Value)
                return;
        }
        else Interlocked.Increment(ref _generation);
        await _gate.WaitAsync();
        try { await storage.ClearAsync(); }
        finally { _gate.Release(); }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        try { Changed?.Invoke(); }
        catch (Exception ex)
        {
            logger.LogWarning("Authentication UI notification failed. FailureType: {FailureType}", ex.GetType().Name);
        }
    }
}
