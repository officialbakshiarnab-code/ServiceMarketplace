namespace ServiceMarketplace.Application.DTOs;

// Purpose: Registration attempt result for new users.
public sealed class AuthRegisterResult(bool succeeded, string? error, IReadOnlyList<AuthErrorDto>? errors)
{
    public bool Succeeded { get; } = succeeded;
    public string? Error { get; } = error;
    public IReadOnlyList<AuthErrorDto>? Errors { get; } = errors;
}
