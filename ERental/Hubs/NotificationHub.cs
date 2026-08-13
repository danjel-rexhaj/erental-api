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
        await base.OnConnectedAsync();
    }

    public Task JoinCarGroup(int carId) => Groups.AddToGroupAsync(Context.ConnectionId, $"car-{carId}");

    public Task LeaveCarGroup(int carId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"car-{carId}");
}