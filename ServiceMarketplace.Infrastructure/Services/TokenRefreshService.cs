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
    private const int IdempotencyWindowSeconds = 30;
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
        string refreshToken,
        string? ipAddress,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Refresh token is required");

        var tokenHash = HashToken(refreshToken);

        var tokenEntity = await dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (tokenEntity == null)
        {
            logger.LogWarning("[TokenRefreshService] Refresh token not found");
            throw new InvalidOperationException("Refresh token is invalid or has been revoked");
        }

        if (!tokenEntity.IsActive)
        {
            if (await DetectTokenReuseAsync(refreshToken))
            {
                logger.LogError(
                    "[TokenRefreshService] Token reuse detected for user {UserId}. Revoking family {TokenFamily}",
                    tokenEntity.UserId, tokenEntity.TokenFamily);

                await RevokeFamilyAsync(tokenEntity.TokenFamily, "TokenReuseDetected");
            }

            throw new InvalidOperationException("Refresh token has expired or been revoked");
        }

        if (tokenEntity.LastUsedAt.HasValue &&
            (DateTime.UtcNow - tokenEntity.LastUsedAt).Value.TotalSeconds < IdempotencyWindowSeconds)
        {
            logger.LogInformation(
                "[TokenRefreshService] Duplicate refresh within idempotency window for user {UserId}",
                tokenEntity.UserId);
        }

        var revokeEntity = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (revokeEntity != null)
        {
            revokeEntity.RevokedAt = DateTime.UtcNow;
            revokeEntity.RevocationReason = "RotatedAtRefresh";
            revokeEntity.LastUsedAt = DateTime.UtcNow;
            revokeEntity.LastUsedIpAddress = ipAddress;
            await dbContext.SaveChangesAsync();
        }

        var newTokenValue = GenerateRandomToken();
        var newTokenHash = HashToken(newTokenValue);

        var newTokenEntity = new RefreshTokenEntity
        {
            UserId = tokenEntity.UserId,
            SessionId = tokenEntity.SessionId,
            TokenHash = newTokenHash,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
            TokenFamily = tokenEntity.TokenFamily,
            IssuedFromIpAddress = ipAddress,
            IssuedFromUserAgent = userAgent
        };

        dbContext.RefreshTokens.Add(newTokenEntity);
        await dbContext.SaveChangesAsync();

        var newAccessToken = await GenerateAccessTokenAsync(tokenEntity.UserId, tokenEntity.SessionId);
        var expirationTime = DateTime.UtcNow.AddMinutes(10);

        return new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            AccessTokenExpiresAt = expirationTime,
            RefreshToken = newTokenValue,
            RefreshTokenExpiresAt = newTokenEntity.ExpiresAt
        };
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
