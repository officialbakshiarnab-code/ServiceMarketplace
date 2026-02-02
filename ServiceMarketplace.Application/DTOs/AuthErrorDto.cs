namespace ServiceMarketplace.Application.DTOs;

// Purpose: Structured authentication error details.
public sealed class AuthErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
