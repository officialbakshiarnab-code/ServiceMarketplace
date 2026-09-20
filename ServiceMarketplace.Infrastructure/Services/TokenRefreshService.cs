using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Data.Extensions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class TokenRefreshService(
    AppDbContext dbContext,
    IConfiguration configuration,
    ILogger<TokenRefreshService> logger) : ITokenRefreshService
{
    private const int RefreshTokenLifetimeDays = 7;
    private const int TokenHashLength = 32;

    public async Task<string> IssueRefreshTokenAsync(
        string userId,
        string sessionId,
        string? ipAddress,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("UserId and SessionId are required");

        var tokenValue = GenerateRandomToken();
        var tokenHash = HashToken(tokenValue);
        var tokenFamily = Guid.NewGuid().ToString("N");

        var refreshToken = new RefreshTokenEntity
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = tokenHash,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
            TokenFamily = tokenFamily,
            IssuedFromIpAddress = ipAddress,
            IssuedFromUserAgent = userAgent
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "[TokenRefreshService] Issued refresh token for user {UserId}, session {SessionId}, family {TokenFamily}",
            userId, sessionId, tokenFamily);

        return tokenValue;
    }

    public async Task<RefreshTokenResponse> RefreshAccessTokenAsync(
        string refreshToken, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Refresh token is required");
        var tokenHash = HashToken(refreshToken);

        var result = await dbContext.ExecuteAtomicAsync<RefreshTokenResponse?>(async () =>
        {
            // Serialize consumption of the same token across API instances. Client-side
            // coordination avoids normal races; this also protects direct API callers.
            var query = dbContext.Database.IsNpgsql()
                ? dbContext.RefreshTokens.FromSqlInterpolated(
                    $"SELECT * FROM \"RefreshTokens\" WHERE \"TokenHash\" = {tokenHash} FOR UPDATE")
                : dbContext.RefreshTokens.AsQueryable();
            var tokenEntity = await query.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            if (tokenEntity == null)
            {
                logger.LogWarning("Refresh token was not found");
                return null;
            }
            if (!tokenEntity.IsActive)
            {
                if (tokenEntity.RevokedAt.HasValue)
                {
                    logger.LogWarning("Refresh token reuse detected. UserId: {UserId}, Family: {TokenFamily}",
                        tokenEntity.UserId, tokenEntity.TokenFamily);
                    await RevokeFamilyAsync(tokenEntity.TokenFamily, "TokenReuseDetected");
                }
                // Return a rejected outcome so revocation commits before the caller gets 401.
                return null;
            }

            var newAccessToken = await GenerateAccessTokenAsync(tokenEntity.UserId, tokenEntity.SessionId);
            var newTokenValue = GenerateRandomToken();
            var now = DateTime.UtcNow;
            tokenEntity.RevokedAt = now;
            tokenEntity.RevocationReason = "RotatedAtRefresh";
            tokenEntity.LastUsedAt = now;
            tokenEntity.LastUsedIpAddress = ipAddress;
            var replacement = new RefreshTokenEntity
            {
                UserId = tokenEntity.UserId, SessionId = tokenEntity.SessionId,
                TokenHash = HashToken(newTokenValue), IssuedAt = now,
                ExpiresAt = now.AddDays(RefreshTokenLifetimeDays), TokenFamily = tokenEntity.TokenFamily,
                IssuedFromIpAddress = ipAddress, IssuedFromUserAgent = userAgent
            };
            dbContext.RefreshTokens.Add(replacement);
            // Revocation and replacement are atomic: a failed insert cannot strand the session.
            await dbContext.SaveChangesAsync();
            return new RefreshTokenResponse
            {
                AccessToken = newAccessToken, AccessTokenExpiresAt = now.AddMinutes(10),
                RefreshToken = newTokenValue, RefreshTokenExpiresAt = replacement.ExpiresAt
            };
        });
        return result ?? throw new InvalidOperationException("Refresh token is invalid, expired or revoked");
    }

    public async Task RevokeAllTokensForUserAsync(string userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required");

        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.RevokedAt.HasValue)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevocationReason = reason;
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task RevokeTokenAsync(string refreshToken, string reason)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required");

        var tokenHash = HashToken(refreshToken);

        var tokenEntity = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (tokenEntity != null && !tokenEntity.RevokedAt.HasValue)
        {
            tokenEntity.RevokedAt = DateTime.UtcNow;
            tokenEntity.RevocationReason = reason;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> DetectTokenReuseAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);

        return await dbContext.RefreshTokens
            .AsNoTracking()
            .AnyAsync(t => t.TokenHash == tokenHash && t.RevokedAt.HasValue);
    }

    public async Task<int> CleanupExpiredTokensAsync()
    {
        var now = DateTime.UtcNow;

        var expiredTokens = await dbContext.RefreshTokens
            .Where(t => t.ExpiresAt < now)
            .ToListAsync();

        dbContext.RefreshTokens.RemoveRange(expiredTokens);
        await dbContext.SaveChangesAsync();

        return expiredTokens.Count;
    }

    private async Task RevokeFamilyAsync(string tokenFamily, string reason)
    {
        var familyTokens = await dbContext.RefreshTokens
            .Where(t => t.TokenFamily == tokenFamily && !t.RevokedAt.HasValue)
            .ToListAsync();

        foreach (var token in familyTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevocationReason = reason;
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<string> GenerateAccessTokenAsync(string userId, string sessionId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new InvalidOperationException("Invalid user id");

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userGuid);

        if (user == null)
            throw new InvalidOperationException("User not found");

        var roles = user.UserRoles
            .Select(ur => ur.Role.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0)
            roles.Add(user.UserType.GetPrimaryRole());

        var expirationTime = DateTime.UtcNow.AddMinutes(10);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, sessionId),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("UserType", ((short)user.UserType).ToString())
        };

        var primaryRole = roles.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(primaryRole))
            claims.Add(new Claim("role", primaryRole));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var capability in await GetMarketplaceCapabilitiesAsync(user.Id, roles))
            claims.Add(new Claim(MarketplaceCapabilityConstants.ClaimType, capability));

        foreach (var permission in AdministrativePermissionConstants.FromRoles(roles))
            claims.Add(new Claim(AdministrativePermissionConstants.ClaimType, permission));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expirationTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<IReadOnlyList<string>> GetMarketplaceCapabilitiesAsync(Guid userId, IReadOnlyCollection<string> roles)
    {
        var capabilities = MarketplaceCapabilityConstants.FromRoles(roles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var approvedSeller = await dbContext.SellerProfiles
            .AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.Status == SellerApplicationStatus.Approved);

        if (approvedSeller)
            capabilities.Add(MarketplaceCapabilityConstants.ProductSeller);

        return capabilities.ToList();
    }

    private static string GenerateRandomToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenHashLength);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }
}
