using ERental.Hubs;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CreateCarSuggestionDto(int CompanyId, string Type, string? Marka, string SuggestedValue);

[ApiController]
[Route("api/[controller]")]
public class CarSuggestionsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IHubContext<NotificationHub> _hub;

    public CarSuggestionsController(ERentalDbContext context, IHubContext<NotificationHub> hub)
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

    // Fired alongside car creation/edit whenever the business picks "Tjeter" for brand or model --
    // never blocks the car itself (the custom text is saved on the car regardless), this is purely
    // an FYI ping so the admin can later add it as a real option in the shared brand/model list.
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateCarSuggestionDto dto)
    {
        var userId = GetUserId();

        if (dto.Type != "brand" && dto.Type != "model")
            return BadRequest("Lloj i panjohur sugjerimi.");
        if (string.IsNullOrWhiteSpace(dto.SuggestedValue))
            return BadRequest("Mungon vlera e sugjeruar.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == dto.CompanyId);
        if (company == null) return BadRequest("Biznesi nuk ekziston.");
        if (company.OwnerUserId != userId) return Forbid();

        var suggestion = new CarSuggestion
        {
            CompanyId = dto.CompanyId,
            Type = dto.Type,
            Marka = dto.Marka,
            SuggestedValue = dto.SuggestedValue.Trim(),
        };

        _context.CarSuggestions.Add(suggestion);
        await _context.SaveChangesAsync();

        try
        {
            var label = dto.Type == "brand" ? $"marke te re: {suggestion.SuggestedValue}" : $"model te ri per {dto.Marka}: {suggestion.SuggestedValue}";
            await NotifyAsync(1, "Sugjerim i ri makine", $"{company.Emri} sugjeroi {label}.", "admin_car_suggestion");
        }
        catch (Exception ex) { Console.WriteLine($"Car suggestion admin notify error: {ex.Message}"); }

        return Ok(suggestion);
    }

    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPending()
    {
        if (GetUserId() != 1) return Forbid();

        var pending = await _context.CarSuggestions
            .Include(s => s.Company)
            .Where(s => s.Statusi == "pending")
            .OrderBy(s => s.DataKrijimit)
            .Select(s => new { s.Id, s.Type, s.Marka, s.SuggestedValue, s.DataKrijimit, CompanyEmri = s.Company.Emri })
            .ToListAsync();

        return Ok(pending);
    }

    [HttpPut("{id}/approve")]
    [Authorize]
    public async Task<IActionResult> Approve(int id)
    {
        if (GetUserId() != 1) return Forbid();

        var suggestion = await _context.CarSuggestions.FindAsync(id);
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

        var suggestion = await _context.CarSuggestions.FindAsync(id);
        if (suggestion == null) return NotFound();

        suggestion.Statusi = "rejected";
        await _context.SaveChangesAsync();

        return Ok(suggestion);
    }
}
