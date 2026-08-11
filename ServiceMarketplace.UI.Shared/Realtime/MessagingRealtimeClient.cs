using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Realtime;

public sealed class MessagingRealtimeClient(
    HttpClient httpClient,
    ITokenStorage tokenStorage,
    ILogger<MessagingRealtimeClient> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _connection;

    public event Func<ConversationRealtimeUpdateDto, Task>? MessageCreated;
    public event Func<ConversationRealtimeUpdateDto, Task>? ConversationUpdated;
    public event Func<int, Task>? UnreadCountChanged;
    public event Func<string, Task>? ConnectionStatusChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _connection ??= CreateConnection();

            if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            await _connection.StartAsync(cancellationToken);
            await DispatchConnectionStatusAsync("Connected");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Messaging realtime connection could not start.");
            await DispatchConnectionStatusAsync("Offline");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection != null)
            {
                await _connection.StopAsync(cancellationToken);
                await DispatchConnectionStatusAsync("Offline");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private HubConnection CreateConnection()
    {
        if (httpClient.BaseAddress == null)
            throw new InvalidOperationException("API base address is not configured.");

        var hubUri = new Uri(httpClient.BaseAddress, "hubs/messaging");
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = tokenStorage.GetTokenAsync;
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<ConversationRealtimeUpdateDto>("MessageCreated", update =>
            DispatchAsync(MessageCreated, update));

        connection.On<ConversationRealtimeUpdateDto>("ConversationUpdated", update =>
            DispatchAsync(ConversationUpdated, update));

        connection.On<int>("UnreadCountChanged", count =>
            DispatchAsync(UnreadCountChanged, count));

        connection.Reconnecting += async _ =>
        {
            await DispatchConnectionStatusAsync("Reconnecting");
        };

        connection.Reconnected += async _ =>
        {
            await DispatchConnectionStatusAsync("Connected");
        };

        connection.Closed += async ex =>
        {
            if (ex != null)
                logger.LogWarning(ex, "Messaging realtime connection closed.");

            await DispatchConnectionStatusAsync("Offline");
        };

        return connection;
    }

    private async Task DispatchConnectionStatusAsync(string status)
    {
        await DispatchAsync(ConnectionStatusChanged, status);
    }

    private async Task DispatchAsync<T>(Func<T, Task>? handler, T payload)
    {
        if (handler == null)
            return;

        foreach (Func<T, Task> subscriber in handler.GetInvocationList())
        {
            try
            {
                await subscriber(payload);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Messaging realtime event subscriber failed.");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();

        _connectionLock.Dispose();
    }
}
