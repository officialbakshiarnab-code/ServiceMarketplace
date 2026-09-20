using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// HTTP client for authentication API endpoints.
/// Handles login/register requests and JWT token persistence.
/// 
/// GRACEFUL ERROR HANDLING:
/// - Never throws InvalidOperationException for API failures
/// - Parses API error responses into user-friendly messages
/// - Treats non-200 responses gracefully
/// - Returns result objects with success/error information
/// </summary>
public sealed class AuthApiClient(HttpClient httpClient, ILogger<AuthApiClient> logger, SessionTokenRefreshService session)
{
    private const long MaxGovernmentIdFileSizeBytes = 5 * 1024 * 1024;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AuthApiClient> _logger = logger;

    /// <summary>
    /// Result of login operation - includes success status and user-friendly error message.
    /// Never throws; always returns a result.
    /// </summary>
    public sealed class LoginResult
    {
        public bool Success { get; set; }
        public AuthResponse? Payload { get; set; }
        public string? Error { get; set; }

        public static LoginResult Successful(AuthResponse payload) => new()
        {
            Success = true,
            Payload = payload,
            Error = null
        };

        public static LoginResult Failed(string error) => new()
        {
            Success = false,
            Payload = null,
            Error = error
        };
    }

    /// <summary>
    /// Result of registration operation - includes success status and user-friendly error message.
    /// </summary>
    public sealed class RegisterResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }

        public static RegisterResult Successful() => new()
        {
            Success = true,
            Error = null
        };

        public static RegisterResult Failed(string error) => new()
        {
            Success = false,
            Error = error
        };
    }

    /// <summary>
    /// Authenticates user and persists JWT token to storage.
    /// 
    /// GRACEFUL ERROR HANDLING:
    /// - Never throws exceptions for auth failures
    /// - Returns LoginResult with success flag and error message
    /// - Parses API error responses into user-friendly text
    /// - Handles network failures gracefully
    /// 
    /// Guarantees:
    /// - If successful: Token persisted to storage
    /// - If failed: No token stored, error message provided
    /// - Returns immediately without throwing
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LoginResult with success flag and error/payload</returns>
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[AuthApiClient] Login attempt for {Email}", request.Email);

            using var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await TryReadErrorAsync(response, cancellationToken) 
                    ?? $"Login failed with status {response.StatusCode}";
                
                _logger.LogWarning("[AuthApiClient] Login failed: {Error}", errorMessage);
                return LoginResult.Failed(errorMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);

            if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
            {
                const string error = "Login response was empty or invalid";
                _logger.LogError("[AuthApiClient] {Error}", error);
                return LoginResult.Failed(error);
            }

            await session.SetTokensAsync(payload.Token, payload.RefreshToken);

            _logger.LogInformation("[AuthApiClient] Login successful for {Email}", request.Email);
            return LoginResult.Successful(payload);
        }
        catch (HttpRequestException ex)
        {
            var error = $"Network error during login: {ex.Message}";
            _logger.LogError(ex, "[AuthApiClient] {Error}", error);
            return LoginResult.Failed(error);
        }
        catch (TaskCanceledException ex)
        {
            var error = "Login request timed out. Please try again.";
            _logger.LogError(ex, "[AuthApiClient] {Error}", error);
            return LoginResult.Failed(error);
        }
        catch (Exception ex)
        {
            var error = "An unexpected error occurred during login. Please try again.";
            _logger.LogError(ex, "[AuthApiClient] Unexpected error: {Message}", ex.Message);
            return LoginResult.Failed(error);
        }
    }

    /// <summary>
    /// Registers a new user account.
    /// 
    /// GRACEFUL ERROR HANDLING:
    /// - Never throws exceptions for validation errors
    /// - Returns RegisterResult with success flag and error message
    /// - Parses API validation errors into user-friendly text
    /// </summary>
    /// <param name="request">Registration details including email, password, and role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RegisterResult with success flag and optional error message</returns>
    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        IBrowserFile? governmentIdImage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[AuthApiClient] Registration attempt for {Email}", request.Email);

            using var multipartContent = governmentIdImage is null
                ? null
                : CreateMultipartRegisterContent(request, governmentIdImage);

            using var response = governmentIdImage is null
                ? await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken)
                : await _httpClient.PostAsync(
                    "api/auth/register",
                    multipartContent,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await TryReadErrorAsync(response, cancellationToken)
                    ?? $"Registration failed with status {response.StatusCode}";
                
                _logger.LogWarning("[AuthApiClient] Registration failed: {Error}", errorMessage);
                return RegisterResult.Failed(errorMessage);
            }

            _logger.LogInformation("[AuthApiClient] Registration successful for {Email}", request.Email);
            return RegisterResult.Successful();
        }
        catch (HttpRequestException ex)
        {
            var error = $"Network error during registration: {ex.Message}";
            _logger.LogError(ex, "[AuthApiClient] {Error}", error);
            return RegisterResult.Failed(error);
        }
        catch (TaskCanceledException ex)
        {
            var error = "Registration request timed out. Please try again.";
            _logger.LogError(ex, "[AuthApiClient] {Error}", error);
            return RegisterResult.Failed(error);
        }
        catch (Exception ex)
        {
            var error = "An unexpected error occurred during registration. Please try again.";
            _logger.LogError(ex, "[AuthApiClient] Unexpected error: {Message}", ex.Message);
            return RegisterResult.Failed(error);
        }
    }

    private static MultipartFormDataContent CreateMultipartRegisterContent(RegisterRequest request, IBrowserFile governmentIdImage)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(request.Email), nameof(RegisterRequest.Email) },
            { new StringContent(request.Password), nameof(RegisterRequest.Password) },
            { new StringContent(request.FirstName), nameof(RegisterRequest.FirstName) },
            { new StringContent(request.LastName), nameof(RegisterRequest.LastName) },
            { new StringContent(request.DateOfBirth.ToString("yyyy-MM-dd")), nameof(RegisterRequest.DateOfBirth) },
            { new StringContent(request.Role), nameof(RegisterRequest.Role) },
            { new StringContent(request.PhoneNumber), nameof(RegisterRequest.PhoneNumber) }
        };

        var fileContent = new StreamContent(governmentIdImage.OpenReadStream(MaxGovernmentIdFileSizeBytes));
        if (!string.IsNullOrWhiteSpace(governmentIdImage.ContentType))
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(governmentIdImage.ContentType);

        content.Add(fileContent, "GovernmentIdImage", governmentIdImage.Name);
        return content;
    }

    /// <summary>
    /// Logs out the current user by calling API endpoint and clearing local token storage.
    /// 
    /// CRITICAL ORDER OF OPERATIONS:
    /// 1. Call POST /api/auth/logout endpoint (AuthorizingHttpClientHandler attaches token automatically)
    ///    - This creates a NEW AuditLogs row with EventType="Logout"
    ///    - Endpoint is [Authorize] protected, requires valid JWT
    /// 2. Clear token from local storage (in finally block)
    ///    - Ensures token is always cleared, even if API fails
    /// 
    /// AUDIT GUARANTEE:
    /// - If API call succeeds: Audit record is created
    /// - If API call fails: Local logout still succeeds (user experience)
    /// 
    /// APPEND-ONLY DESIGN:
    /// - Each logout creates exactly ONE new row in AuditLogs
    /// - NEVER updates existing rows
    /// - EventType="Logout", TimestampUtc=DateTime.UtcNow
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if logout completed (API call may have failed but local logout succeeded)</returns>
    public async Task<bool> LogoutAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;

        try
        {
            _logger.LogInformation("[AuthApiClient] Calling logout API...");

            // AuthorizingHttpClientHandler automatically attaches Bearer token
            // No need to manually get token and attach it
            response = await _httpClient.PostAsync("api/auth/logout", null, cancellationToken);

            // STEP 4: Check response status
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[AuthApiClient] Logout API call succeeded - audit record created");
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[AuthApiClient] Logout API call failed: {StatusCode}, Details: {Error}",
                response.StatusCode, errorContent);
            return true; // Still consider logout successful locally even if API failed
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[AuthApiClient] Logout API network error (non-fatal): {Message}", ex.Message);
            return true; // Network failure shouldn't prevent local logout
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "[AuthApiClient] Logout API request timed out (non-fatal): {Message}", ex.Message);
            return true; // Timeout shouldn't prevent local logout
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthApiClient] Logout API exception (non-fatal): {Message}", ex.Message);
            return true; // Any exception shouldn't prevent local logout
        }
        finally
        {
            // STEP 5: Dispose response if it was created
            response?.Dispose();

            // STEP 6: Always clear token from storage, regardless of API success
            // This ensures user can always logout from the UI perspective
            _logger.LogInformation("[AuthApiClient] Clearing token from storage...");
            await session.SignOutAsync();
            _logger.LogInformation("[AuthApiClient] Token cleared from storage - logout complete");
        }
    }

    /// <summary>
    /// Attempts to extract user-friendly error message from API response.
    /// Handles various error formats: simple strings, validation errors, problem details.
    /// Never throws; always returns a string or null.
    /// </summary>
    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            // Try "message" field (common in custom responses)
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString();

            // Try "error" field (common in error responses)
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();

            // Try "title" field (ProblemDetails format)
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();

            // Try "errors" field (FluentValidation format)
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var lines = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var messages = prop.Value.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        var joined = string.Join("; ", messages!);
                        if (!string.IsNullOrWhiteSpace(joined))
                            lines.Add($"{prop.Name}: {joined}");
                    }
                }

                if (lines.Count > 0)
                    return string.Join(" | ", lines);
            }

            return null;
        }
        catch
        {
            return null; // Parsing failed, return null to use default error message
        }
    }
}
