namespace ServiceMarketplace.Application.DTOs;

public sealed class AdminProvisioningResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public Guid? UserId { get; init; }
    public bool CreatedUser { get; init; }
    public bool GrantedAdminRole { get; init; }
    public bool PasswordReset { get; init; }

    public static AdminProvisioningResult Failed(string message)
    {
        return new AdminProvisioningResult
        {
            Success = false,
            Message = message
        };
    }
}
