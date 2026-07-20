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
    private readonly IPrivateFileService _privateFileService;
    public UsersController(ERentalDbContext context, IFileUploadService fileUploadService, IPrivateFileService privateFileService)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _privateFileService = privateFileService;
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
            HasLicensePara = !string.IsNullOrWhiteSpace(user.PatentaFotoPara),
            HasLicenseMbrapa = !string.IsNullOrWhiteSpace(user.PatentaFotoMbrapa),
            WhatsappVerified = user.WhatsappVerified ?? false,
            WhatsappStatus = latestWhatsapp?.Statusi
        });
    }

    // Uploads go to the private R2 bucket via IPrivateFileService, which stores an object key
    // rather than a public URL — the photo is only ever readable back through GetMyLicensePhoto
    // below (or the booking-scoped equivalent in BookingsController), never as a shareable link.
    // The client-side booking flow gates the pay button on HasLicensePara/Mbrapa — checked again
    // server-side in PaymentsController.CreateOrder so it can't be skipped by bypassing the UI.
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
            user.PatentaFotoPara = await _privateFileService.UploadAsync(stream, para.FileName, para.ContentType, $"users/{userId}/patenta");
        }
        if (mbrapa != null && mbrapa.Length > 0)
        {
            using var stream = mbrapa.OpenReadStream();
            user.PatentaFotoMbrapa = await _privateFileService.UploadAsync(stream, mbrapa.FileName, mbrapa.ContentType, $"users/{userId}/patenta");
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            HasLicensePara = !string.IsNullOrWhiteSpace(user.PatentaFotoPara),
            HasLicenseMbrapa = !string.IsNullOrWhiteSpace(user.PatentaFotoMbrapa)
        });
    }

    [HttpGet("me/license/{side}")]
    [Authorize]
    public async Task<IActionResult> GetMyLicensePhoto(string side)
    {
        var userId = GetUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var key = side == "para" ? user.PatentaFotoPara : side == "mbrapa" ? user.PatentaFotoMbrapa : null;
        if (string.IsNullOrWhiteSpace(key)) return NotFound();

        var (stream, contentType) = await _privateFileService.DownloadAsync(key);
        return File(stream, contentType ?? "image/jpeg");
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
