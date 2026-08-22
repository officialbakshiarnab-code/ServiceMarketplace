namespace ServiceMarketplace.UI.Shared.Auth;

public interface IRegistrationIntentStorage
{
    Task SaveAsync(RegistrationIntentState intent);
    Task<RegistrationIntentState?> GetAsync();
    Task ClearAsync();
}
