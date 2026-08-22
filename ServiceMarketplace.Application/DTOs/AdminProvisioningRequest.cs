namespace ServiceMarketplace.Application.DTOs;

public sealed class AdminProvisioningRequest
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string PhoneNumber { get; init; }
    public required string Password { get; init; }
    public required string Reason { get; init; }
    public bool ResetPassword { get; init; }
    public string? OperatorUserId { get; init; }
}
