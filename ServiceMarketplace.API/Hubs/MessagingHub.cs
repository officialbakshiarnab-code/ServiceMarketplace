using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Hubs;

[Authorize]
public sealed class MessagingHub : Hub<IMessagingClient>
{
}
