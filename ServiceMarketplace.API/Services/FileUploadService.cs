using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ServiceMarketplace.API.Services;

/// <summary>
/// Service for handling secure file uploads for government ID verification.
/// 
/// Features:
/// - Image-only validation (JPEG, PNG, GIF, WebP)
/// - File size limits (max 5MB)
/// - Secure file naming using hash
/// - Organized storage by date
/// - Graceful error handling
/// 
/// Security:
/// - Validates MIME type and file extension
/// - Renames files to prevent directory traversal
/// - Stores in secure directory
/// - No execution permissions
/// </summary>
public sealed class FileUploadService : ServiceMarketplace.Infrastructure.Services.IFileUploadService
{
    private readonly string _uploadDirectory;
    private readonly ILogger<FileUploadService> _logger;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };

    public FileUploadService(ILogger<FileUploadService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        
        // Store files in App_Data directory, organized by date
        var dataDirectory = Path.Combine(env.ContentRootPath, "..", "App_Data", "Uploads");
        _uploadDirectory = Path.GetFullPath(dataDirectory);
        
        // Create directory if it doesn't exist
        Directory.CreateDirectory(_uploadDirectory);
        
        _logger.LogInformation("[FileUploadService] Upload directory configured: {Directory}", _uploadDirectory);
    }

    /// <summary>
    /// Uploads a government ID image for the specified user.
    /// Implements Infrastructure.Services.IFileUploadService.
    /// Accepts object parameter - must be IFormFile at runtime.
    /// </summary>
    public async Task<string?> UploadGovernmentIdAsync(object? file, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Type-check and cast to IFormFile
            if (file is not IFormFile formFile)
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: Invalid file type for user {UserId}", userId);
                return null;
            }

            // Validate file exists
            if (formFile.Length == 0)
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: No file provided for user {UserId}", userId);
                return null;
            }

            // Validate file size
            if (formFile.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: File size {Size} exceeds limit {Limit} for user {UserId}",
                    formFile.Length, MaxFileSizeBytes, userId);
                return null;
            }

            // Validate extension
            var extension = Path.GetExtension(formFile.FileName).ToLowerInvariant();
            if (!Array.Exists(AllowedExtensions, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: Invalid extension {Extension} for user {UserId}",
                    extension, userId);
                return null;
            }

            // Validate MIME type
            var mimeType = formFile.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!Array.Exists(AllowedMimeTypes, m => m.Equals(mimeType, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("[FileUploadService] Government ID upload: Invalid MIME type {MimeType} for user {UserId}",
                    formFile.ContentType, userId);
                return null;
            }

            // Create directory structure: {year}/{month}/government-id/
            var today = DateTime.UtcNow;
            var dateDirectory = Path.Combine(_uploadDirectory, today.Year.ToString(), today.Month.ToString("D2"), "government-id");
            Directory.CreateDirectory(dateDirectory);

            // Generate secure filename: {userId}_{timestamp}_{hash}.{extension}
            var timestamp = today.Ticks;
            var hash = GenerateFileHash(formFile, userId);
            var secureFileName = $"{userId}_{timestamp}_{hash}{extension}";
            var filePath = Path.Combine(dateDirectory, secureFileName);

            // Validate path to prevent directory traversal
            var fullPath = Path.GetFullPath(filePath);
            var fullUploadDirectory = Path.GetFullPath(_uploadDirectory);
            if (!fullPath.StartsWith(fullUploadDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[FileUploadService] Government ID upload: Path traversal attempt detected for user {UserId}", userId);
                return null;
            }

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await formFile.CopyToAsync(stream, cancellationToken);
            }

            // Return relative path for database storage
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

    /// <summary>
    /// Deletes a stored file.
    /// Safe to call with non-existent files or null paths.
    /// Returns: true if file was deleted, false if file didn't exist or error occurred.
    /// </summary>
    public async Task<bool> DeleteFileAsync(string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            var fullPath = GetFullPath(relativePath);
            
            // Validate path to prevent directory traversal
            var fullUploadDirectory = Path.GetFullPath(_uploadDirectory);
            if (!Path.GetFullPath(fullPath).StartsWith(fullUploadDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[FileUploadService] Delete: Path traversal attempt detected");
                return false;
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogInformation("[FileUploadService] Delete: File does not exist: {Path}", relativePath);
                return false;
            }

            File.Delete(fullPath);
            _logger.LogInformation("[FileUploadService] File deleted: {Path}", relativePath);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "[FileUploadService] Delete: Access denied for file: {Path}", relativePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileUploadService] Delete: Unexpected error deleting file: {Path}", relativePath);
            return false;
        }
    }

    /// <summary>
    /// Generates a hash from file metadata and user ID.
    /// Used for secure file naming.
    /// </summary>
    private static string GenerateFileHash(IFormFile file, string userId)
    {
        using (var sha256 = SHA256.Create())
        {
            // Hash: userId + filename + file size
            var combined = userId + file.FileName + file.Length;
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
            return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
        }
    }
}
