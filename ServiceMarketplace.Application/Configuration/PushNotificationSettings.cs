namespace ServiceMarketplace.Application.Configuration;

public sealed class PushNotificationSettings
{
    public const string SectionName = "PushNotifications";
    public const string ProviderNoop = "Noop";
    public const string ProviderFcm = "Fcm";

    public string Provider { get; set; } = ProviderNoop;
    public string FirebaseProjectId { get; set; } = string.Empty;
    public string FirebaseServiceAccountJsonPath { get; set; } = string.Empty;

    public bool UseFcm => string.Equals(Provider, ProviderFcm, StringComparison.OrdinalIgnoreCase);

    public void ValidateForFcm()
    {
        if (!UseFcm)
            return;

        if (string.IsNullOrWhiteSpace(FirebaseProjectId))
            throw new InvalidOperationException("PushNotifications:FirebaseProjectId is required when PushNotifications:Provider is Fcm.");

        if (string.IsNullOrWhiteSpace(FirebaseServiceAccountJsonPath))
            throw new InvalidOperationException("PushNotifications:FirebaseServiceAccountJsonPath is required when PushNotifications:Provider is Fcm.");
    }
}
