using ERental.Application.Interfaces;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record UpdateMeDto(string Emri, string Mbiemri, string? Telefoni, bool HasWhatsapp);
public record AdminUpdateUserDto(string Emri, string Mbiemri, string? Telefoni, string Email);

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

        try
        {
            var (stream, contentType) = await _privateFileService.DownloadAsync(key);
            return File(stream, contentType ?? "image/jpeg");
        }
        catch (Amazon.S3.AmazonS3Exception)
        {
            return NotFound();
        }
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

    // Anonymizes rather than hard-deletes the row -- booking history a business needs for its own
    // records stays intact and referentially valid, but the account can no longer be logged into
    // and carries no personal data anymore. No schema change: reuses existing columns.
    [HttpDelete("me")]
    [Authorize]
    public async Task<IActionResult> DeleteMe()
    {
        var userId = GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        if (await _context.Companies.AnyAsync(c => c.OwnerUserId == userId))
            return BadRequest("Ke nje biznes te regjistruar -- kontakto support per te fshire llogarine.");

        user.Emri = "Perdorues";
        user.Mbiemri = "i fshire";
        user.Email = $"deleted-{userId}@erental.store";
        user.Telefoni = null;
        user.FotoProfili = null;
        user.PatentaFotoPara = null;
        user.PatentaFotoMbrapa = null;
        user.HasWhatsapp = false;
        user.WhatsappVerified = false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

        _context.WhatsappVerifications.RemoveRange(_context.WhatsappVerifications.Where(w => w.UserId == userId));
        _context.EmailVerifications.RemoveRange(_context.EmailVerifications.Where(e => e.UserId == userId));
        _context.Notifications.RemoveRange(_context.Notifications.Where(n => n.UserId == userId));

        await _context.SaveChangesAsync();
        return Ok(new { message = "Llogaria u fshi." });
    }

    // Admin-only hard delete for clearing out test/junk accounts -- unlike DeleteMe (which
    // anonymizes and blocks if the caller owns a company), this actually removes the row and, if
    // the user owns a company, cascades through that company exactly like Companies/{id}/force
    // does. Staged children-first since several FKs here are RESTRICT at the DB level and
    // Notification.BookingId isn't modeled in EF.
    [HttpDelete("{id}/force")]
    [Authorize]
    public async Task<IActionResult> ForceDeleteUser(int id)
    {
        if (GetUserId() != 1) return Forbid();
        if (id == 1) return BadRequest("S'mund te fshihet llogaria e adminit.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();

        using var transaction = await _context.Database.BeginTransactionAsync();

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == id);
        if (company != null)
        {
            var carIds = await _context.Cars.Where(c => c.CompanyId == company.CompanyId).Select(c => c.CarId).ToListAsync();
            var companyBookingIds = await _context.Bookings.Where(b => carIds.Contains(b.CarId)).Select(b => b.BookingId).ToListAsync();

            _context.Payments.RemoveRange(_context.Payments.Where(p => companyBookingIds.Contains(p.BookingId)));
            _context.Reviews.RemoveRange(_context.Reviews.Where(r => companyBookingIds.Contains(r.BookingId) || r.CompanyId == company.CompanyId));
            _context.LicenseViews.RemoveRange(_context.LicenseViews.Where(v => companyBookingIds.Contains(v.BookingId)));
            _context.Notifications.RemoveRange(_context.Notifications.Where(n => n.BookingId != null && companyBookingIds.Contains(n.BookingId.Value)));
            await _context.SaveChangesAsync();

            _context.Bookings.RemoveRange(_context.Bookings.Where(b => carIds.Contains(b.CarId)));
            await _context.SaveChangesAsync();

            _context.CarPhotos.RemoveRange(_context.CarPhotos.Where(p => carIds.Contains(p.CarId)));
            _context.CarPriceOffers.RemoveRange(_context.CarPriceOffers.Where(o => carIds.Contains(o.CarId)));
            _context.CarAvailabilityBlocks.RemoveRange(_context.CarAvailabilityBlocks.Where(b => carIds.Contains(b.CarId)));
            _context.CarViews.RemoveRange(_context.CarViews.Where(v => carIds.Contains(v.CarId)));
            _context.Favorites.RemoveRange(_context.Favorites.Where(f => carIds.Contains(f.CarId)));
            await _context.SaveChangesAsync();

            _context.Cars.RemoveRange(_context.Cars.Where(c => c.CompanyId == company.CompanyId));
            await _context.SaveChangesAsync();

            _context.CompanyVerifications.RemoveRange(_context.CompanyVerifications.Where(v => v.CompanyId == company.CompanyId));
            _context.CompanySubscriptions.RemoveRange(_context.CompanySubscriptions.Where(s => s.CompanyId == company.CompanyId));
            _context.AmenitySuggestions.RemoveRange(_context.AmenitySuggestions.Where(a => a.CompanyId == company.CompanyId));
            await _context.SaveChangesAsync();

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
        }

        var bookingIds = await _context.Bookings.Where(b => b.UserId == id).Select(b => b.BookingId).ToListAsync();
        _context.Payments.RemoveRange(_context.Payments.Where(p => bookingIds.Contains(p.BookingId)));
        _context.Reviews.RemoveRange(_context.Reviews.Where(r => bookingIds.Contains(r.BookingId) || r.UserId == id));
        _context.LicenseViews.RemoveRange(_context.LicenseViews.Where(v => bookingIds.Contains(v.BookingId) || v.ViewedByUserId == id));
        _context.Notifications.RemoveRange(_context.Notifications.Where(n => n.UserId == id || (n.BookingId != null && bookingIds.Contains(n.BookingId.Value))));
        await _context.SaveChangesAsync();

        _context.Bookings.RemoveRange(_context.Bookings.Where(b => b.UserId == id));
        await _context.SaveChangesAsync();

        _context.Favorites.RemoveRange(_context.Favorites.Where(f => f.UserId == id));
        _context.LoginLogs.RemoveRange(_context.LoginLogs.Where(l => l.UserId == id));
        _context.CarViews.RemoveRange(_context.CarViews.Where(v => v.UserId == id));
        _context.WhatsappVerifications.RemoveRange(_context.WhatsappVerifications.Where(w => w.UserId == id));
        _context.EmailVerifications.RemoveRange(_context.EmailVerifications.Where(e => e.UserId == id));
        await _context.SaveChangesAsync();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(new { message = "Perdoruesi u fshi plotesisht." });
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

        var newEmail = dto.Email.Trim().ToLowerInvariant();
        if (newEmail != user.Email)
        {
            var taken = await _context.Users.AnyAsync(u => u.UserId != id && u.Email == newEmail);
            if (taken) return BadRequest("Ky email eshte ne perdorim nga nje llogari tjeter.");
            user.Email = newEmail;
        }

        user.Emri = dto.Emri;
        user.Mbiemri = dto.Mbiemri;
        user.Telefoni = dto.Telefoni;
        await _context.SaveChangesAsync();

        return Ok(new { user.UserId, user.Emri, user.Mbiemri, user.Telefoni, user.Email });
    }
}
