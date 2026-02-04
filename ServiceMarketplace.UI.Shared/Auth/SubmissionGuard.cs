namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Prevents duplicate form submissions by tracking submission keys.
/// 
/// USAGE:
/// 1. Create instance per form: var guard = new SubmissionGuard();
/// 2. Call IsAllowedAsync() before submitting: if (!await guard.IsAllowedAsync(key)) return;
/// 3. Guard automatically resets after timeout (default 30 seconds)
/// 4. Or manually reset: guard.Reset();
/// 
/// GUARANTEES:
/// - Only one submission per key within timeout window
/// - Automatic cleanup prevents memory leaks
/// - Works with async operations
/// - Prevents race conditions from rapid button clicks
/// </summary>
public sealed class SubmissionGuard
{
    private readonly Dictionary<string, SubmissionState> _submissions = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _timeout;

    public SubmissionGuard(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(30); // Default 30 second window
    }

    /// <summary>
    /// Checks if a submission with the given key is allowed.
    /// Returns false if a submission with this key is already in progress.
    /// </summary>
    /// <param name="submissionKey">Unique identifier for the submission (e.g., "login", "register")</param>
    /// <returns>True if submission is allowed, false if already in progress</returns>
    public async Task<bool> IsAllowedAsync(string submissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionKey);

        await _semaphore.WaitAsync();
        try
        {
            // Check if submission already in progress
            if (_submissions.TryGetValue(submissionKey, out var state))
            {
                // Check if it has expired
                if (DateTime.UtcNow < state.ExpiresAt)
                {
                    return false; // Still in progress, not allowed
                }

                // Expired, remove it
                _submissions.Remove(submissionKey);
            }

            // Allow submission and mark it as in progress
            _submissions[submissionKey] = new SubmissionState
            {
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_timeout)
            };

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Manually resets submission tracking for a specific key.
    /// </summary>
    public async Task ResetAsync(string submissionKey)
    {
        await _semaphore.WaitAsync();
        try
        {
            _submissions.Remove(submissionKey);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Resets all submission tracking.
    /// Useful when clearing auth state or logging out.
    /// </summary>
    public async Task ResetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _submissions.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Internal class tracking submission state.
    /// </summary>
    private sealed class SubmissionState
    {
        public DateTime StartedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
