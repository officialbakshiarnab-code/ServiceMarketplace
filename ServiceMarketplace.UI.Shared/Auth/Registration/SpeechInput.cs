namespace ServiceMarketplace.UI.Shared.Auth.Registration;

public sealed record SpeechInputResult(
    bool Succeeded,
    string? Transcript = null,
    string? Error = null);

public interface ISpeechInputService
{
    bool IsAvailable { get; }
    Task<SpeechInputResult> CaptureAsync(CancellationToken cancellationToken = default);
}

public sealed class UnavailableSpeechInputService : ISpeechInputService
{
    public bool IsAvailable => false;

    public Task<SpeechInputResult> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SpeechInputResult(false, Error: "Voice input is not available yet."));
    }
}
