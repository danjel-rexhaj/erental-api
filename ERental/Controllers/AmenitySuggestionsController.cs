using ERental.Hubs;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CreateAmenitySuggestionDto(int CompanyId, string Suggestion);

[ApiController]
[Route("api/[controller]")]
public class AmenitySuggestionsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IHubContext<NotificationHub> _hub;

    public AmenitySuggestionsController(ERentalDbContext context, IHubContext<NotificationHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
    public async Task<IActionResult> Create(CreateAmenitySuggestionDto dto)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(dto.Suggestion))
            return BadRequest("Shkruaj nje pershkrim per pajisjen qe sugjeron.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == dto.CompanyId);
        if (company == null) return BadRequest("Biznesi nuk ekziston.");
        if (company.OwnerUserId != userId) return Forbid();

        var suggestion = new AmenitySuggestion
        {
            CompanyId = dto.CompanyId,
            Suggestion = dto.Suggestion.Trim(),
        };

        _context.AmenitySuggestions.Add(suggestion);
        await _context.SaveChangesAsync();

        try
        {
            await NotifyAsync(1, "Sugjerim i ri pajisjeje", $"{company.Emri} sugjeroi: {suggestion.Suggestion}", "admin_amenity_suggestion");
        }
        catch (Exception ex) { Console.WriteLine($"Amenity suggestion admin notify error: {ex.Message}"); }

        return Ok(suggestion);
    }

    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPending()
    {
        if (GetUserId() != 1) return Forbid();

        var pending = await _context.AmenitySuggestions
            .Include(s => s.Company)
            .Where(s => s.Statusi == "pending")
            .OrderBy(s => s.DataKrijimit)
            .Select(s => new { s.Id, s.Suggestion, s.DataKrijimit, CompanyEmri = s.Company.Emri })
            .ToListAsync();

        return Ok(pending);
    }

    [HttpPut("{id}/approve")]
    [Authorize]
    public async Task<IActionResult> Approve(int id)
    {
        if (GetUserId() != 1) return Forbid();

        var suggestion = await _context.AmenitySuggestions.FindAsync(id);
        if (suggestion == null) return NotFound();

        suggestion.Statusi = "approved";
        await _context.SaveChangesAsync();

        return Ok(suggestion);
    }

    [HttpPut("{id}/reject")]
    [Authorize]
    public async Task<IActionResult> Reject(int id)
    {
        if (GetUserId() != 1) return Forbid();

        var suggestion = await _context.AmenitySuggestions.FindAsync(id);
        if (suggestion == null) return NotFound();

        suggestion.Statusi = "rejected";
        await _context.SaveChangesAsync();

        return Ok(suggestion);
    }
}
