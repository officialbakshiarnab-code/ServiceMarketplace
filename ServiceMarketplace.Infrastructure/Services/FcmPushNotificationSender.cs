using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceMarketplace.Application.Configuration;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class FcmPushNotificationSender(
    HttpClient httpClient,
    IOptions<PushNotificationSettings> options,
    ILogger<FcmPushNotificationSender> logger) : IPushNotificationSender
{
    private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    public async Task SendAsync(string deviceToken, PushNotificationPayloadDto payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return;

        var settings = options.Value;
        settings.ValidateForFcm();

        var accessToken = await GetAccessTokenAsync(settings.FirebaseServiceAccountJsonPath, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(settings.FirebaseProjectId)}/messages:send");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                token = deviceToken,
                notification = new
                {
                    title = "ServiceMarketplace",
                    body = "New message about your service order"
                },
                data = new Dictionary<string, string>
                {
                    ["type"] = payload.Type,
                    ["conversationId"] = payload.ConversationId?.ToString("D") ?? string.Empty,
                    ["messageId"] = payload.MessageId?.ToString("D") ?? string.Empty
                },
                android = new
                {
                    priority = "HIGH"
                },
                apns = new
                {
                    payload = new
                    {
                        aps = new
                        {
                            sound = "default"
                        }
                    }
                }
            }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "FCM push notification failed. Status: {StatusCode}, Response: {Response}",
            response.StatusCode,
            responseBody);

        var shouldDeactivateDevice = IsInvalidDeviceTokenResponse(responseBody);
        throw new PushNotificationDeliveryException(
            $"FCM push notification failed with status {(int)response.StatusCode}.",
            shouldDeactivateDevice);
    }

    private static async Task<string> GetAccessTokenAsync(string serviceAccountJsonPath, CancellationToken cancellationToken)
    {
        var credential = (await CredentialFactory
            .FromFileAsync(
                serviceAccountJsonPath,
                JsonCredentialParameters.ServiceAccountCredentialType,
                cancellationToken))
            .CreateScoped(FirebaseMessagingScope);

        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }

    private static bool IsInvalidDeviceTokenResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return ContainsTokenErrorCode(document.RootElement);
        }
        catch (JsonException)
        {
            return responseBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool ContainsTokenErrorCode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("errorCode") &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    IsDeviceTokenError(property.Value.GetString()))
                {
                    return true;
                }

                if (ContainsTokenErrorCode(property.Value))
                    return true;
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsTokenErrorCode(item))
                    return true;
            }
        }

        return false;
    }

    private static bool IsDeviceTokenError(string? errorCode)
    {
        return string.Equals(errorCode, "UNREGISTERED", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(errorCode, "SENDER_ID_MISMATCH", StringComparison.OrdinalIgnoreCase);
    }
}
