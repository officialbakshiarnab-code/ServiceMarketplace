using System.Threading;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Manages AuthenticationStateProvider initialization to ensure it happens exactly once.
/// Prevents concurrent initialization attempts and race conditions during startup.
///
/// GUARANTEES:
/// - InitializeAsync() completes exactly once, regardless of concurrent calls
/// - All subsequent calls await the first initialization
/// - Thread-safe and Blazor-compatible
/// - No deadlocks or blocking operations
/// </summary>
public sealed class AuthenticationStateInitializer
{
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private Task? _initializationTask;
    private bool _isInitialized;

    /// <summary>
    /// Initializes authentication state exactly once.
    /// Subsequent calls await the first initialization to complete.
    /// </summary>
    /// <param name="initializeFunc">Async function to perform initialization</param>
    /// <returns>Task that completes when initialization finishes</returns>
    public async Task InitializeAsync(Func<Task> initializeFunc)
    {
        if (_isInitialized)
            return; // Already initialized, return immediately

        await _initializationSemaphore.WaitAsync();
        try
        {
            // Double-check pattern: another thread might have initialized while we waited
            if (_isInitialized)
                return;

            // Perform initialization
            _initializationTask = initializeFunc();
            await _initializationTask;

            _isInitialized = true;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets whether initialization has completed.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Resets initialization state (useful for testing or re-initialization).
    /// CAUTION: Only call during logout or testing!
    /// </summary>
    public void Reset()
    {
        _isInitialized = false;
        _initializationTask = null;
    }
}
