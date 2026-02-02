namespace ServiceMarketplace.Application.DTOs;

// Purpose: Authentication result payload (token + expiration).
public sealed class AuthResultDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
