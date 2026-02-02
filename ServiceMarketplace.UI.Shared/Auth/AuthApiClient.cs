using System.Net.Http.Json;
using System.Text.Json;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// HTTP client for authentication API endpoints.
/// Handles login/register requests and JWT token persistence.
/// </summary>
public sealed class AuthApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    /// <summary>
    /// Authenticates user and persists JWT token to storage.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication response with token and metadata</returns>
    /// <exception cref="InvalidOperationException">When login fails or response is invalid</exception>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await TryReadErrorAsync(response, cancellationToken) ?? "Login failed.";
            throw new InvalidOperationException(errorMessage);
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        
        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
            throw new InvalidOperationException("Login response was empty.");

        // Persist JWT for future authenticated requests
        await _tokenStorage.SaveTokenAsync(payload.Token);
        
        return payload;
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Registration details including email, password, and role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">When registration fails</exception>
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await TryReadErrorAsync(response, cancellationToken) ?? "Registration failed.";
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Attempts to extract user-friendly error message from API response.
    /// Handles various error formats: simple strings, validation errors, problem details.
    /// </summary>
    /// <summary>
    /// Logs out the current user by calling API endpoint and clearing local token storage.
    /// 
    /// CRITICAL ORDER OF OPERATIONS:
    /// 1. Call POST /api/auth/logout endpoint (while token is still valid)
    ///    - This creates a NEW AuditLogs row with EventType="Logout"
    ///    - Endpoint is [Authorize] protected, requires valid JWT
    /// 2. Clear token from local storage (happens in finally block)
    ///    - Ensures token is always cleared, even if API fails
    /// 
    /// AUDIT GUARANTEE:
    /// - If API call succeeds: Audit record is created ?
    /// - If API call fails: Local logout still succeeds (user experience)
    /// 
    /// APPEND-ONLY DESIGN:
    /// - Each logout creates exactly ONE new row in AuditLogs
    /// - NEVER updates existing rows
    /// - EventType="Logout", TimestampUtc=DateTime.UtcNow
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;
        
        try
        {
            // STEP 1: Get token BEFORE making the API call
            var token = await _tokenStorage.GetTokenAsync();
            
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("[AuthApiClient] No token found, skipping API logout call");
                return;
            }
            
            // STEP 2: Attach Bearer token to request header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            Console.WriteLine("[AuthApiClient] Calling logout API with Bearer token...");
            
            // STEP 3: Call logout API (while token is still valid and attached)
            response = await _httpClient.PostAsync("api/auth/logout", null, cancellationToken);
            
            // STEP 4: Check response status
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[AuthApiClient] Logout API call succeeded - audit record created");
            }
            else
            {
                Console.WriteLine($"[AuthApiClient] Logout API call failed: {response.StatusCode}");
                
                // Try to read error details
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(errorContent))
                {
                    Console.WriteLine($"[AuthApiClient] Error details: {errorContent}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - network failure shouldn't prevent local logout
            Console.WriteLine($"[AuthApiClient] Logout API exception: {ex.Message}");
        }
        finally
        {
            // STEP 5: Clean up HTTP client headers
            _httpClient.DefaultRequestHeaders.Authorization = null;
            
            // STEP 6: Dispose response if it was created
            response?.Dispose();
            
            // STEP 7: Always clear token from storage, regardless of API success
            // This ensures user can always logout from the UI perspective
            Console.WriteLine("[AuthApiClient] Clearing token from storage...");
            await _tokenStorage.ClearAsync();
            Console.WriteLine("[AuthApiClient] Token cleared from storage");
        }
    }

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

            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString();

            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();

            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();

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
            return null;
        }
    }
}