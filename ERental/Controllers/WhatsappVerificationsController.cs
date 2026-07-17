using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsappVerificationsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    public WhatsappVerificationsController(ERentalDbContext context) => _context = context;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
