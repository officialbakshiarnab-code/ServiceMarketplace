namespace ServiceMarketplace.UI.Shared.Auth;

public sealed class RegistrationIntentState
{
    public bool WantsServices { get; set; }
    public bool WantsProducts { get; set; }
    public bool WantsProvider { get; set; }
    public bool WantsSeller { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
