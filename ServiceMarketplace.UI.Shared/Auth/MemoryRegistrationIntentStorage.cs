namespace ServiceMarketplace.UI.Shared.Auth;

public sealed class MemoryRegistrationIntentStorage : IRegistrationIntentStorage
{
    private RegistrationIntentState? _intent;

    public Task SaveAsync(RegistrationIntentState intent)
    {
        _intent = intent;
        return Task.CompletedTask;
    }

    public Task<RegistrationIntentState?> GetAsync()
    {
        return Task.FromResult(_intent);
    }

    public Task ClearAsync()
    {
        _intent = null;
        return Task.CompletedTask;
    }
}
