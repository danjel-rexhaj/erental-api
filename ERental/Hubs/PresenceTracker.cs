using System.Collections.Concurrent;

namespace ERental.Hubs;

// Every open tab holds a NotificationHub connection (logged in or not), so the live connection
// set *is* "who's on the site right now". Kept in memory only: it's correct for the single Render
// instance and naturally empties on restart, when every client reconnects anyway.
public class PresenceInfo
{
    public string ConnectionId { get; init; } = "";
    public int? UserId { get; init; }
    public string? Email { get; init; }
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public DateTime ConnectedAt { get; init; }
    public string? Page { get; set; }
    public DateTime LastSeen { get; set; }
}

public static class PresenceTracker
{
    private static readonly ConcurrentDictionary<string, PresenceInfo> _connections = new();

    public static void Add(PresenceInfo info) => _connections[info.ConnectionId] = info;

    public static void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public static void SetPage(string connectionId, string? page)
    {
        if (_connections.TryGetValue(connectionId, out var info))
        {
            info.Page = page?.Length > 200 ? page[..200] : page;
            info.LastSeen = DateTime.UtcNow;
        }
    }

    public static List<PresenceInfo> Snapshot() => _connections.Values.ToList();
}
