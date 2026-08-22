using ERental.Application.Interfaces;
using ERental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;
using WebPush;

namespace ERental.Infrastructure.Services;

// Deliberately NOT `using ERental.Infrastructure.Entities;` here -- that namespace's
// PushSubscription entity would collide with WebPush's own PushSubscription class. The entity is
// only ever touched through _context.PushSubscriptions (a DbSet property, no bare type name
// needed), so this file never needs the alias.
public class PushService : IPushService
{
    private readonly ERentalDbContext _context;
    private readonly WebPushClient _client = new();
    private readonly VapidDetails _vapidDetails;

    public PushService(ERentalDbContext context, IConfiguration config)
    {
        _context = context;
        _vapidDetails = new VapidDetails(config["Vapid:Subject"], config["Vapid:PublicKey"], config["Vapid:PrivateKey"]);
    }

    // Mirrors the frontend's App.jsx handleNotificationClick switch -- kept in sync manually since
    // the service worker (which opens this URL on tap) can't reach into the app's own router code.
    private static string TargetToUrl(string? target) => target switch
    {
        "business_booking" => "/biznesi?tab=bookings",
        "client_booking" or "leave_review" => "/rezervimet",
        "admin_company_verification" => "/biznesi?tab=admin",
        "admin_whatsapp_verification" => "/biznesi?tab=whatsapp",
        "admin_license_verification" => "/biznesi?tab=patenta",
        "admin_amenity_suggestion" => "/biznesi?tab=amenity-suggestions",
        "admin_car_suggestion" => "/biznesi?tab=car-suggestions",
        "whatsapp_verified" or "license_verified" or "license_rejected" => "/profili",
        _ => "/",
    };

    public async Task SendToUserAsync(int userId, string title, string message, string? target = null)
    {
        var subs = await _context.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
        if (subs.Count == 0) return;

        var payload = JsonSerializer.Serialize(new { title, body = message, url = TargetToUrl(target) });
        var toRemove = new List<int>();

        foreach (var sub in subs)
        {
            var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            try
            {
                await _client.SendNotificationAsync(pushSubscription, payload, _vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Browser/OS dropped this subscription (uninstalled, permission revoked, etc.) --
                // prune it so future sends don't keep paying the round-trip to find that out again.
                toRemove.Add(sub.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Push send error for user {userId}: {ex.Message}");
            }
        }

        if (toRemove.Count > 0)
        {
            var stale = await _context.PushSubscriptions.Where(s => toRemove.Contains(s.Id)).ToListAsync();
            _context.PushSubscriptions.RemoveRange(stale);
        }

        await _context.SaveChangesAsync();
    }
}
