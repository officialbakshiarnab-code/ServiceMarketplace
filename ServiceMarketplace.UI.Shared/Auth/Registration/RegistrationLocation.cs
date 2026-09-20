using Microsoft.JSInterop;

namespace ServiceMarketplace.UI.Shared.Auth.Registration;

public interface IRegistrationLocationService
{
    bool IsAvailable { get; }
    Task<RegistrationLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default);
}

public sealed record ReverseGeocodingResult(bool Succeeded, string? Label = null, string? Error = null);

public interface IReverseGeocodingService
{
    Task<ReverseGeocodingResult> ResolveAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

public sealed class UnavailableReverseGeocodingService : IReverseGeocodingService
{
    public Task<ReverseGeocodingResult> ResolveAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReverseGeocodingResult(false, Error: "Reverse geocoding is not configured."));
}

public sealed record RegistrationLocationResult(
    bool Succeeded,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null,
    string? MapUrl = null,
    string? Error = null)
{
    public string ToAddressText()
    {
        if (!Succeeded || Latitude is null || Longitude is null)
            return string.Empty;

        var lat = Latitude.Value.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
        var lng = Longitude.Value.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
        var accuracy = AccuracyMeters is > 0
            ? $" Accuracy about {AccuracyMeters.Value:0} meters."
            : string.Empty;

        return $"Current location: {lat}, {lng}.{accuracy}";
    }
}

public sealed class UnavailableRegistrationLocationService : IRegistrationLocationService
{
    public bool IsAvailable => false;

    public Task<RegistrationLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new RegistrationLocationResult(false, Error: "Current location is not available in this build."));
}

public sealed class BrowserRegistrationLocationService(IJSRuntime jsRuntime) : IRegistrationLocationService
{
    public bool IsAvailable => true;

    public async Task<RegistrationLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<BrowserLocationResult>(
                "serviceMarketplaceRegistrationLocation.getCurrentPosition",
                cancellationToken);

            if (result is not { Succeeded: true } || result.Latitude is null || result.Longitude is null)
                return new RegistrationLocationResult(false, Error: result?.Error ?? "Location permission was denied or unavailable.");

            var mapUrl = BuildMapUrl(result.Latitude.Value, result.Longitude.Value);
            return new RegistrationLocationResult(true, result.Latitude, result.Longitude, result.AccuracyMeters, mapUrl);
        }
        catch (JSException ex)
        {
            return new RegistrationLocationResult(false, Error: ex.Message);
        }
    }

    private static string BuildMapUrl(double latitude, double longitude)
    {
        var lat = latitude.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
        var lng = longitude.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
        return $"https://www.openstreetmap.org/?mlat={lat}&mlon={lng}#map=16/{lat}/{lng}";
    }

    private sealed class BrowserLocationResult
    {
        public bool Succeeded { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public string? Error { get; set; }
    }
}
