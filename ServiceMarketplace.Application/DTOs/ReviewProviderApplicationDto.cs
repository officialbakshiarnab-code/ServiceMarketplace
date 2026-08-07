using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ReviewProviderApplicationDto
{
    public ProviderApplicationStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
}
