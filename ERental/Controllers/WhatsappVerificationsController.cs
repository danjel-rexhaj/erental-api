using ERental.Application.Interfaces;
using ERental.Hubs;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsappVerificationsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IHubContext<NotificationHub> _hub;

    public WhatsappVerificationsController(ERentalDbContext context, IEmailService emailService, IHubContext<NotificationHub> hub)
    {
        _context = context;
        _emailService = emailService;
        _hub = hub;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task NotifyAsync(int userId, string title, string message, string? target = null)
    {
        var notif = new Notification { UserId = userId, Title = title, Message = message, IsRead = false, Target = target };
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync();

        await _hub.Clients.Group(userId.ToString()).SendAsync("notification", new
        {
            id = notif.Id,
            title = notif.Title,
            message = notif.Message,
            createdAt = notif.DataKrijimit,
            bookingId = notif.BookingId,
            target = notif.Target
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> RequestVerification()
    {
        var userId = GetUserId();

        var pending = await _context.WhatsappVerifications.Where(w => w.UserId == userId && w.Statusi == "pending").ToListAsync();
        _context.WhatsappVerifications.RemoveRange(pending);

        var code = new Random().Next(1000, 9999).ToString();
        var verification = new WhatsappVerification { UserId = userId, Code = code, Statusi = "pending" };
        _context.WhatsappVerifications.Add(verification);

        await _context.SaveChangesAsync();

        try
        {
            var requester = await _context.Users.FindAsync(userId);
            if (requester != null)
            {
                await NotifyAsync(1, "Kerkese verifikimi WhatsApp", $"{requester.Emri} {requester.Mbiemri} kerkoi verifikim te numrit WhatsApp.", "admin_whatsapp_verification");

                var admin = await _context.Users.FindAsync(1);
                if (admin?.Email != null)
                    await _emailService.SendAdminWhatsappVerificationRequestAsync(admin.Email, $"{requester.Emri} {requester.Mbiemri}", requester.Telefoni);
            }
        }
        catch (Exception ex) { Console.WriteLine($"WhatsApp verification admin notify error: {ex.Message}"); }

        return Ok(new { code });
    }

    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPending()
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        var pending = await _context.WhatsappVerifications
            .Include(w => w.User)
            .Where(w => w.Statusi == "pending")
            .OrderBy(w => w.DataKrijimit)
            .Select(w => new
            {
                w.Id,
                w.Code,
                w.DataKrijimit,
                Emri = w.User.Emri,
                Mbiemri = w.User.Mbiemri,
                Email = w.User.Email,
                Telefoni = w.User.Telefoni
            })
            .ToListAsync();

        return Ok(pending);
    }

    [HttpPut("{id}/verify")]
    [Authorize]
    public async Task<IActionResult> Verify(int id)
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        var verification = await _context.WhatsappVerifications.Include(w => w.User).FirstOrDefaultAsync(w => w.Id == id);
        if (verification == null) return NotFound();

        verification.Statusi = "verified";
        verification.DataShqyrtimit = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        verification.User.WhatsappVerified = true;

        await _context.SaveChangesAsync();
        return Ok(new { verified = true });
    }

    [HttpPut("{id}/reject")]
    [Authorize]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        var verification = await _context.WhatsappVerifications.FirstOrDefaultAsync(w => w.Id == id);
        if (verification == null) return NotFound();

        verification.Statusi = "rejected";
        verification.DataShqyrtimit = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();
        return Ok(new { rejected = true });
    }
}
