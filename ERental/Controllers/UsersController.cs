using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record UpdateMeDto(string Emri, string Mbiemri, string? Telefoni, bool HasWhatsapp);

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ERentalDbContext _context;
    public UsersController(ERentalDbContext context) => _context = context;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        var latestWhatsapp = await _context.WhatsappVerifications
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.DataKrijimit)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            user.Emri,
            user.Mbiemri,
            user.Telefoni,
            user.HasWhatsapp,
            WhatsappVerified = user.WhatsappVerified ?? false,
            WhatsappStatus = latestWhatsapp?.Statusi
        });
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe(UpdateMeDto dto)
    {
        var userId = GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        user.Emri = dto.Emri;
        user.Mbiemri = dto.Mbiemri;
        user.Telefoni = dto.Telefoni;
        user.HasWhatsapp = dto.HasWhatsapp;

        await _context.SaveChangesAsync();

        return Ok(new { user.Emri, user.Mbiemri, user.Telefoni, user.HasWhatsapp });
    }
}
