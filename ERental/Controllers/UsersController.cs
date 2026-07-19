using ERental.Application.Interfaces;
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
    private readonly IFileUploadService _fileUploadService;
    public UsersController(ERentalDbContext context, IFileUploadService fileUploadService)
    {
        _context = context;
        _fileUploadService = fileUploadService;
    }

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
            user.FotoProfili,
            user.DataRegjistrimit,
            WhatsappVerified = user.WhatsappVerified ?? false,
            WhatsappStatus = latestWhatsapp?.Statusi
        });
    }

    [HttpPost("me/photo")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Nuk u dergua asnje file.");

        var userId = GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        using var stream = file.OpenReadStream();
        var url = await _fileUploadService.UploadAsync(stream, file.FileName, file.ContentType, $"users/{userId}");

        user.FotoProfili = url;
        await _context.SaveChangesAsync();

        return Ok(new { fotoProfili = url });
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
