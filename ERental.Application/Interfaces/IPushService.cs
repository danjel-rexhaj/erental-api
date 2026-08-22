namespace ERental.Application.Interfaces;

public interface IPushService
{
    // Fire-and-forget from the caller's perspective: never throws for an individual expired/failed
    // subscription (those are pruned from the DB instead) so a push failure never blocks the
    // in-app SignalR notification it accompanies.
    Task SendToUserAsync(int userId, string title, string message, string? target = null);
}
