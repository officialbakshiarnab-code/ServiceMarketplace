namespace ServiceMarketplace.Application.DTOs;

// Purpose: Authentication attempt result for login.
public sealed class AuthLoginResult(bool succeeded, AuthResultDto? payload, string? error)
{
    public bool Succeeded { get; } = succeeded;
    public AuthResultDto? Payload { get; } = payload;
    public string? Error { get; } = error;
}
