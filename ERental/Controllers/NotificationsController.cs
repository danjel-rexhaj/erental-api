using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    public NotificationsController(ERentalDbContext context) => _context = context;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userId = GetUserId();
        var list = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.DataKrijimit)
            .Take(30)
            .ToListAsync();
        return Ok(list);
    }

    [HttpPut("mark-read")]
    [Authorize]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetUserId();
        var unread = await _context.Notifications.Where(n => n.UserId == userId && n.IsRead == false).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var userId = GetUserId();
        var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notif == null) return NotFound();

        _context.Notifications.Remove(notif);
        await _context.SaveChangesAsync();
        return Ok(new { deleted = true });
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> DeleteAllNotifications()
    {
        var userId = GetUserId();
        var mine = await _context.Notifications.Where(n => n.UserId == userId).ToListAsync();
        _context.Notifications.RemoveRange(mine);
        await _context.SaveChangesAsync();
        return Ok(new { deleted = mine.Count });
    }
}