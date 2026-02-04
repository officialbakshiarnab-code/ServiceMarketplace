namespace ServiceMarketplace.Infrastructure.Services;

/// <summary>
/// Wrapper interface for file upload service.
/// Abstracts away ASP.NET-specific IFormFile from the application layer.
/// Implementation resides in API layer through dependency injection.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads a government ID image file securely.
    /// Accepts object parameter that should be an IFormFile at runtime.
    /// 
    /// Returns: relative file path on success, null on failure
    /// Failures are logged but don't throw exceptions.
    /// </summary>
    Task<string?> UploadGovernmentIdAsync(object? file, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full file path from a relative path.
    /// </summary>
    string GetFullPath(string relativePath);

    /// <summary>
    /// Deletes a stored file.
    /// </summary>
    Task<bool> DeleteFileAsync(string relativePath);
}
