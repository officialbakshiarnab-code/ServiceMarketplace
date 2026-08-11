using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ResolveServiceOrderDisputeDto
{
    public ServiceOrderDisputeStatus Status { get; set; }
    public string ResolutionNotes { get; set; } = string.Empty;
}
