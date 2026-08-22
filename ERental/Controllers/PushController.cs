using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace ERental.Controllers;

public record SubscribePushDto(string Endpoint, string P256dh, string Auth, string? UserAgent);

[ApiController]
[Route("api/[controller]")]
public class PushController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IConfiguration _config;

    public PushController(ERentalDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Not secret -- this is the public half of the VAPID keypair, meant to be handed to the
    // browser's pushManager.subscribe(). Kept server-side (not baked into the frontend build) so
    // rotating it never requires a frontend redeploy.
    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        return Ok(new { publicKey = _config["Vapid:PublicKey"] });
    }

    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> Subscribe(SubscribePushDto dto)
    {
        var userId = GetUserId();

        var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (existing != null)
        {
            existing.UserId = userId;
            existing.P256dh = dto.P256dh;
            existing.Auth = dto.Auth;
            existing.UserAgent = dto.UserAgent;
        }
        else
        {
            _context.PushSubscriptions.Add(new Infrastructure.Entities.PushSubscription
            {
                UserId = userId,
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth,
                UserAgent = dto.UserAgent,
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { subscribed = true });
    }

    // Endpoint travels as a query param, not a JSON body -- some proxies/CDNs strip the body off
    // DELETE requests entirely (bit us before on Companies/{id}/reject with a 415).
    [HttpDelete("subscribe")]
    [Authorize]
    public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint)
    {
        var userId = GetUserId();

        var sub = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId);
        if (sub != null)
        {
            _context.PushSubscriptions.Remove(sub);
            await _context.SaveChangesAsync();
        }

        return Ok(new { unsubscribed = true });
    }
}
