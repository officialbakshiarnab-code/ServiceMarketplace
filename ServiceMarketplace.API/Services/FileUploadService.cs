using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ServiceMarketplace.API.Services;

/// <summary>
/// Handles secure government ID uploads.
/// </summary>
public sealed class FileUploadService : ServiceMarketplace.Infrastructure.Services.IFileUploadService
{
    private readonly string _uploadDirectory;
    private readonly ILogger<FileUploadService> _logger;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf" };

    public FileUploadService(ILogger<FileUploadService> logger, IWebHostEnvironment env)
    {
        _logger = logger;

        var dataDirectory = Path.Combine(env.ContentRootPath, "..", "App_Data", "Uploads");
        _uploadDirectory = Path.GetFullPath(dataDirectory);

        Directory.CreateDirectory(_uploadDirectory);

        _logger.LogInformation("[FileUploadService] Upload directory configured: {Directory}", _uploadDirectory);
    }

    /// <summary>
    /// Uploads a government ID image for the specified user.
    /// Accepts an IFormFile through the infrastructure contract.
    /// </summary>
    public async Task<string?> UploadGovernmentIdAsync(object? file, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (file is not IFormFile formFile)
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: Invalid file type for user {UserId}", userId);
                return null;
            }

            if (formFile.Length == 0)
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: No file provided for user {UserId}", userId);
                return null;
            }

            if (formFile.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: File size {Size} exceeds limit {Limit} for user {UserId}",
                    formFile.Length, MaxFileSizeBytes, userId);
                return null;
            }

            var extension = Path.GetExtension(formFile.FileName).ToLowerInvariant();
            if (!Array.Exists(AllowedExtensions, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: Invalid extension {Extension} for user {UserId}",
                    extension, userId);
                return null;
            }

            var mimeType = formFile.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!Array.Exists(AllowedMimeTypes, m => m.Equals(mimeType, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: Invalid MIME type {MimeType} for user {UserId}",
                    formFile.ContentType, userId);
                return null;
            }

            var govIdDirectory = Path.Combine(_uploadDirectory, "govid", userId);
            Directory.CreateDirectory(govIdDirectory);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hash = GenerateFileHash(formFile, userId);
            var secureFileName = $"{timestamp}_{hash}{extension}";
            var filePath = Path.Combine(govIdDirectory, secureFileName);

            var fullPath = Path.GetFullPath(filePath);
            var fullUploadDirectory = Path.GetFullPath(_uploadDirectory);
            if (!fullPath.StartsWith(fullUploadDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[FileUploadService] Government ID upload: Path traversal attempt detected for user {UserId}", userId);
                return null;
            }

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await formFile.CopyToAsync(stream, cancellationToken);
            }

            var relativePath = Path.GetRelativePath(_uploadDirectory, filePath);
            _logger.LogInformation("[FileUploadService] Government ID uploaded successfully for user {UserId}: {Path}",
                userId, relativePath);
            
            return relativePath;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[FileUploadService] Government ID upload cancelled for user {UserId}", userId);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "[FileUploadService] I/O error uploading government ID for user {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileUploadService] Unexpected error uploading government ID for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Gets the full file path from a relative path.
    /// </summary>
    public string GetFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return string.Empty;
        
        return Path.Combine(_uploadDirectory, relativePath);
    }

    public Task<bool> DeleteFileAsync(string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return Task.FromResult(false);

            var fullPath = GetFullPath(relativePath);

            var fullUploadDirectory = Path.GetFullPath(_uploadDirectory);
            if (!Path.GetFullPath(fullPath).StartsWith(fullUploadDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[FileUploadService] Delete: Path traversal attempt detected");
                return Task.FromResult(false);
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogInformation("[FileUploadService] Delete: File does not exist: {Path}", relativePath);
                return Task.FromResult(false);
            }

            File.Delete(fullPath);
            _logger.LogInformation("[FileUploadService] File deleted: {Path}", relativePath);
            return Task.FromResult(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "[FileUploadService] Delete: Access denied for file: {Path}", relativePath);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileUploadService] Delete: Unexpected error deleting file: {Path}", relativePath);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Generates a hash from file metadata and user ID.
    /// Used for secure file naming.
    /// </summary>
    private static string GenerateFileHash(IFormFile file, string userId)
    {
        using var sha256 = SHA256.Create();
        var combined = userId + file.FileName + file.Length;
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
    }
}
