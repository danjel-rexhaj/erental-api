using ERental.Application.Interfaces;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record UpdateMeDto(string Emri, string Mbiemri, string? Telefoni, bool HasWhatsapp);
public record AdminUpdateUserDto(string Emri, string Mbiemri, string? Telefoni);

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
            user.PatentaFotoPara,
            user.PatentaFotoMbrapa,
            WhatsappVerified = user.WhatsappVerified ?? false,
            WhatsappStatus = latestWhatsapp?.Statusi
        });
    }

    // The client-side booking flow gates the pay button on this — checked again server-side in
    // PaymentsController.CreateOrder so a request crafted without going through the UI can't skip it.
    [HttpPost("me/license")]
    [Authorize]
    public async Task<IActionResult> UploadLicense(IFormFile? para, IFormFile? mbrapa)
    {
        if ((para == null || para.Length == 0) && (mbrapa == null || mbrapa.Length == 0))
            return BadRequest("Nuk u dergua asnje foto.");

        var userId = GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        if (para != null && para.Length > 0)
        {
            using var stream = para.OpenReadStream();
            user.PatentaFotoPara = await _fileUploadService.UploadAsync(stream, para.FileName, para.ContentType, $"users/{userId}/patenta");
        }
        if (mbrapa != null && mbrapa.Length > 0)
        {
            using var stream = mbrapa.OpenReadStream();
            user.PatentaFotoMbrapa = await _fileUploadService.UploadAsync(stream, mbrapa.FileName, mbrapa.ContentType, $"users/{userId}/patenta");
        }

        await _context.SaveChangesAsync();

        return Ok(new { user.PatentaFotoPara, user.PatentaFotoMbrapa });
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

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetUsers()
    {
        if (GetUserId() != 1) return Forbid();

        var companyOwnerIds = await _context.Companies.Select(c => c.OwnerUserId).ToListAsync();
        var users = await _context.Users
            .OrderByDescending(u => u.DataRegjistrimit)
            .Select(u => new
            {
                u.UserId,
                u.Emri,
                u.Mbiemri,
                u.Email,
                u.Telefoni,
                u.DataRegjistrimit,
                HasCompany = companyOwnerIds.Contains(u.UserId)
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> AdminUpdateUser(int id, AdminUpdateUserDto dto)
    {
        if (GetUserId() != 1) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Emri = dto.Emri;
        user.Mbiemri = dto.Mbiemri;
        user.Telefoni = dto.Telefoni;
        await _context.SaveChangesAsync();

        return Ok(new { user.UserId, user.Emri, user.Mbiemri, user.Telefoni });
    }
}
