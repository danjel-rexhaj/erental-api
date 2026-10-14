using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ERental.Hubs;

// Not [Authorize] -- car availability (JoinCarGroup/LeaveCarGroup below) is public data anyone
// browsing a car's page should get live updates for, logged in or not. Personal notifications
// still only reach authenticated connections, since OnConnectedAsync only joins the per-user
// group when a userId claim is actually present.
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        var http = Context.GetHttpContext();
        PresenceTracker.Add(new PresenceInfo
        {
            ConnectionId = Context.ConnectionId,
            UserId = int.TryParse(userId, out var id) ? id : null,
            Email = Context.User?.FindFirstValue(ClaimTypes.Email),
            Ip = http?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http?.Request.Headers.UserAgent.ToString(),
            ConnectedAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        });
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        PresenceTracker.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public void SetPage(string page) => PresenceTracker.SetPage(Context.ConnectionId, page);

    public Task JoinCarGroup(int carId) => Groups.AddToGroupAsync(Context.ConnectionId, $"car-{carId}");

    public Task LeaveCarGroup(int carId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"car-{carId}");
}
