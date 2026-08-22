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

public record UpdateLocationDto(double Latitude, double Longitude);
public record UpdateCompanyDto(string Emri, string? Telefoni, string? Adresa, string? Qyteti, string? Iban, bool? OfronDergimMakine = null, int? MinimumDitesh = null, decimal? CmimiSigurimit = null);
public record AdminUpdateCompanyDto(string Emri, string? Telefoni, string? Adresa, string? Qyteti, string? Statusi);

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IFileUploadService _fileUploadService;
    private readonly IEmailService _emailService;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IPushService _push;

    public CompaniesController(ERentalDbContext context, IFileUploadService fileUploadService, IEmailService emailService, IHubContext<NotificationHub> hub, IPushService push)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _emailService = emailService;
        _hub = hub;
        _push = push;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Iban is [JsonIgnore] on the entity so it never leaks through the public GetCars/GetCompanies
    // endpoints; this is the one place it's explicitly surfaced, for the company's own owner/admin.
    private static object ProjectCompanyOwner(Company c) => new
    {
        c.CompanyId, c.Emri, c.Email, c.Telefoni, c.Adresa, c.Qyteti, c.Nipt,
        c.EshteVerifikuar, c.DataVerifikimit, c.CommissionRate, c.DataRegjistrimit,
        c.BillingModel, c.Statusi, c.OwnerUserId, c.LogoUrl, c.Latitude, c.Longitude,
        c.AllowCashPayment, c.AvgRating, c.ReviewCount, c.CarCount, c.Iban, c.OfronDergimMakine, c.MinimumDitesh, c.CmimiSigurimit
    };

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

        try { await _push.SendToUserAsync(userId, title, message, target); } catch { }
    }

    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> RegisterCompany(
    [FromForm] string emri, [FromForm] string telefoni, [FromForm] string adresa,
    [FromForm] string qyteti, [FromForm] string nipt, [FromForm] string? iban, [FromForm] double? latitude,
    [FromForm] double? longitude, [FromForm] bool? ofronDergimMakine, IFormFile? certifikataFile)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(emri)) return BadRequest("Emri i biznesit eshte i detyrueshem.");
        if (string.IsNullOrWhiteSpace(telefoni)) return BadRequest("Telefoni eshte i detyrueshem.");
        if (string.IsNullOrWhiteSpace(adresa)) return BadRequest("Adresa eshte e detyrueshme.");
        if (string.IsNullOrWhiteSpace(qyteti)) return BadRequest("Qyteti eshte i detyrueshem.");
        if (string.IsNullOrWhiteSpace(nipt)) return BadRequest("NIPT-i eshte i detyrueshem.");
        if (string.IsNullOrWhiteSpace(iban)) return BadRequest("IBAN eshte i detyrueshem.");
        if (certifikataFile == null || certifikataFile.Length == 0) return BadRequest("Certifikata e NIPT-it eshte e detyrueshme.");

        if (await _context.Companies.AnyAsync(c => c.Nipt == nipt))
            return BadRequest("NIPT-i eshte i regjistruar tashme.");

        var owner = await _context.Users.FindAsync(userId);
        if (owner == null) return NotFound();

        var company = new Company
        {
            Emri = emri,
            Email = owner.Email,
            Telefoni = telefoni,
            Adresa = adresa,
            Qyteti = qyteti,
            Nipt = nipt,
            Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.Trim().Replace(" ", "").ToUpperInvariant(),
            Latitude = latitude,
            Longitude = longitude,
            OfronDergimMakine = ofronDergimMakine ?? false,
            EshteVerifikuar = false,
            BillingModel = "commission",
            Statusi = "active",
            OwnerUserId = userId
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        string? certUrl = null;
        if (certifikataFile != null && certifikataFile.Length > 0)
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var accessKey = config["R2:AccessKey"];
            var secretKey = config["R2:SecretKey"];
            var endpoint = config["R2:Endpoint"];
            var bucketName = config["R2:BucketName"];
            var publicUrl = config["R2:PublicUrl"];

            var s3Config = new Amazon.S3.AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true };
            using var s3Client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);

            var fileName = $"certificates/{company.CompanyId}/{Guid.NewGuid()}{Path.GetExtension(certifikataFile.FileName)}";
            using var stream = certifikataFile.OpenReadStream();
            var putRequest = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = stream,
                ContentType = certifikataFile.ContentType,
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };
            await s3Client.PutObjectAsync(putRequest);
            certUrl = $"{publicUrl}/{fileName}";

            _context.CompanyVerifications.Add(new CompanyVerification
            {
                CompanyId = company.CompanyId,
                Nipt = nipt,
                CertifikataUrl = certUrl,
                Statusi = "pending"
            });
            await _context.SaveChangesAsync();
        }

        // Notify admin regardless of whether a certificate was attached — this used to live inside
        // the certifikataFile block above, so a business that registered without uploading a cert
        // at signup time produced zero admin notification (in-app or email) at all, even though it
        // still showed up in the pending-verification list waiting to be found by chance.
        // In-app notify and email are in separate try/catch blocks -- previously one try wrapped
        // both, so a failure in the bell notification (e.g. a DB constraint) silently killed the
        // email too, even though the two channels have nothing to do with each other.
        try
        {
            var message = certUrl != null
                ? $"{company.Emri} dergoi certifikaten e NIPT-it dhe pret verifikim."
                : $"{company.Emri} u regjistrua dhe pret verifikim.";
            await NotifyAsync(1, "Kerkese verifikimi biznesi", message, "admin_company_verification");
        }
        catch (Exception ex) { Console.WriteLine($"Admin verification notify error: {ex.Message}"); }

        try
        {
            // Hardcoded to the monitored support inbox rather than the userId=1 account's login
            // email, which may not be watched -- a new business registration must always reach here.
            await _emailService.SendAdminVerificationRequestAsync("info@erental.store", company.Emri, company.CompanyId);
        }
        catch (Exception ex) { Console.WriteLine($"Admin verification email error: {ex.Message}"); }

        return Ok(new { company.CompanyId, company.Emri, company.Nipt, Statusi = "Pending verifikim" });
    }

    [HttpGet]
    public async Task<IActionResult> GetCompanies()
    {
        var companies = await _context.Companies.ToListAsync();

        // AvgRating/ReviewCount/CarCount aren't persisted columns kept in sync elsewhere -- compute
        // them here the same way CarsController.AttachCompanyStatsAsync does, so the homepage's
        // verified-businesses cards can show real numbers instead of stale/empty values.
        var companyIds = companies.Select(c => c.CompanyId).ToList();
        var ratingStats = await _context.Reviews
            .Where(r => companyIds.Contains(r.CompanyId) && r.Rating != null)
            .GroupBy(r => r.CompanyId)
            .Select(g => new { CompanyId = g.Key, Avg = g.Average(r => r.Rating!.Value), Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId);
        var carCounts = await _context.Cars
            .Where(c => companyIds.Contains(c.CompanyId) && c.Statusi == "active")
            .GroupBy(c => c.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

        foreach (var company in companies)
        {
            if (ratingStats.TryGetValue(company.CompanyId, out var rs))
            {
                company.AvgRating = Math.Round(rs.Avg, 1);
                company.ReviewCount = rs.Count;
            }
            company.CarCount = carCounts.TryGetValue(company.CompanyId, out var cc) ? cc : 0;
        }

        return Ok(companies);
    }

    [HttpGet("my-company")]
    [Authorize]
    public async Task<IActionResult> GetMyCompany()
    {
        var userId = GetUserId();
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");
        return Ok(ProjectCompanyOwner(company));
    }

    [HttpPost("my-company/logo")]
    [Authorize]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Nuk u dergua asnje file.");

        var userId = GetUserId();
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        using var stream = file.OpenReadStream();
        var url = await _fileUploadService.UploadAsync(stream, file.FileName, file.ContentType, $"companies/{company.CompanyId}");

        company.LogoUrl = url;
        await _context.SaveChangesAsync();

        return Ok(new { logoUrl = url });
    }

    [HttpPut("my-company")]
    [Authorize]
    public async Task<IActionResult> UpdateMyCompany(UpdateCompanyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Emri))
            return BadRequest("Emri i biznesit nuk mund te jete bosh.");

        var userId = GetUserId();
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        company.Emri = dto.Emri.Trim();
        company.Telefoni = dto.Telefoni;
        company.Adresa = dto.Adresa;
        company.Qyteti = dto.Qyteti;
        if (dto.Iban != null)
            company.Iban = string.IsNullOrWhiteSpace(dto.Iban) ? null : dto.Iban.Trim().Replace(" ", "").ToUpperInvariant();
        if (dto.OfronDergimMakine.HasValue)
            company.OfronDergimMakine = dto.OfronDergimMakine.Value;
        company.MinimumDitesh = dto.MinimumDitesh;
        company.CmimiSigurimit = dto.CmimiSigurimit;
        await _context.SaveChangesAsync();

        return Ok(ProjectCompanyOwner(company));
    }

    // Soft delete: nothing is actually removed (existing bookings/invoices/contracts stay intact
    // for accounting purposes), the company and its cars just stop showing up anywhere new
    // bookings could be made, and the business can turn it back on themselves later.
    [HttpPost("my-company/deactivate")]
    [Authorize]
    public async Task<IActionResult> DeactivateMyCompany()
    {
        var userId = GetUserId();
        var company = await _context.Companies.Include(c => c.Cars).FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        company.Statusi = "inactive";
        foreach (var car in company.Cars) car.Statusi = "inactive";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Llogaria u caktivizua." });
    }

    [HttpPost("my-company/reactivate")]
    [Authorize]
    public async Task<IActionResult> ReactivateMyCompany()
    {
        var userId = GetUserId();
        var company = await _context.Companies.Include(c => c.Cars).FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        company.Statusi = "active";
        foreach (var car in company.Cars) car.Statusi = "active";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Llogaria u riaktivizua." });
    }

    [HttpPut("my-company/location")]
    [Authorize]
    public async Task<IActionResult> UpdateLocation(UpdateLocationDto dto)
    {
        var userId = GetUserId();
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        company.Latitude = dto.Latitude;
        company.Longitude = dto.Longitude;
        await _context.SaveChangesAsync();

        return Ok(new { company.Latitude, company.Longitude });
    }

    [HttpPut("{id}/admin")]
    [Authorize]
    public async Task<IActionResult> AdminUpdateCompany(int id, AdminUpdateCompanyDto dto)
    {
        if (GetUserId() != 1) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Emri))
            return BadRequest("Emri i biznesit nuk mund te jete bosh.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company == null) return NotFound();

        company.Emri = dto.Emri.Trim();
        company.Telefoni = dto.Telefoni;
        company.Adresa = dto.Adresa;
        company.Qyteti = dto.Qyteti;
        if (!string.IsNullOrWhiteSpace(dto.Statusi)) company.Statusi = dto.Statusi;
        await _context.SaveChangesAsync();

        return Ok(ProjectCompanyOwner(company));
    }

    [HttpPut("{id}/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyCompany(int id)
    {
        var userId = GetUserId();

        if (userId != 1)
            return Forbid();

        var company = await _context.Companies.Include(c => c.OwnerUser).FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company == null) return NotFound();

        company.EshteVerifikuar = true;
        company.DataVerifikimit = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        company.Statusi = "active";

        await _context.SaveChangesAsync();

        try
        {
            if (company.Email != null)
                await _emailService.SendCompanyVerifiedAsync(company.Email, company.OwnerUser?.Emri ?? "atje", company.Emri);
        }
        catch (Exception ex) { Console.WriteLine($"Company verified email error: {ex.Message}"); }

        return Ok(new { message = "Biznesi u verifikua.", company.EshteVerifikuar, company.CompanyId });
    }

    // Reason travels as a query param, not a JSON body -- some proxies/CDNs strip the body off
    // DELETE requests entirely, which was causing a 415 from the JSON formatter in production.
    [HttpDelete("{id}/reject")]
    [Authorize]
    public async Task<IActionResult> RejectCompany(int id, [FromQuery] string? reason)
    {
        if (GetUserId() != 1) return Forbid();
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest("Arsyeja e refuzimit eshte e detyrueshme.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company == null) return NotFound();
        if (company.EshteVerifikuar == true) return BadRequest("Ky biznes eshte tashme i verifikuar dhe nuk mund te refuzohet.");

        if (await _context.Cars.AnyAsync(c => c.CompanyId == id))
            return BadRequest("Ky biznes ka tashme makina te shtuara dhe nuk mund te refuzohet automatikisht -- kontakto zhvilluesin.");

        var toEmail = company.Email;
        var companyName = company.Emri;

        _context.CompanyVerifications.RemoveRange(_context.CompanyVerifications.Where(v => v.CompanyId == id));
        _context.Companies.Remove(company);
        await _context.SaveChangesAsync();

        try
        {
            if (!string.IsNullOrWhiteSpace(toEmail))
                await _emailService.SendCompanyRejectedAsync(toEmail, companyName, reason);
        }
        catch (Exception ex) { Console.WriteLine($"Company rejected email error: {ex.Message}"); }

        return Ok(new { message = "Kerkesa u refuzua dhe biznesi u fshi." });
    }

    // Unlike Reject (which only handles a still-pending, car-less company), this permanently wipes
    // a company and everything under it -- cars, bookings, payments, reviews, etc. -- regardless of
    // status. Admin-only, for clearing out test/junk data. Deletion is staged children-first, each
    // stage saved before the next, since several FKs here are RESTRICT at the DB level (no cascade)
    // and a couple of relationships (Notification.BookingId) aren't modeled in EF at all, so we
    // can't rely on EF's automatic dependency-graph ordering across a single SaveChanges call.
    [HttpDelete("{id}/force")]
    [Authorize]
    public async Task<IActionResult> ForceDeleteCompany(int id)
    {
        if (GetUserId() != 1) return Forbid();

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company == null) return NotFound();

        var carIds = await _context.Cars.Where(c => c.CompanyId == id).Select(c => c.CarId).ToListAsync();
        var bookingIds = await _context.Bookings.Where(b => carIds.Contains(b.CarId)).Select(b => b.BookingId).ToListAsync();

        using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Payments.RemoveRange(_context.Payments.Where(p => bookingIds.Contains(p.BookingId)));
        _context.Reviews.RemoveRange(_context.Reviews.Where(r => bookingIds.Contains(r.BookingId) || r.CompanyId == id));
        _context.LicenseViews.RemoveRange(_context.LicenseViews.Where(v => bookingIds.Contains(v.BookingId)));
        _context.Notifications.RemoveRange(_context.Notifications.Where(n => n.BookingId != null && bookingIds.Contains(n.BookingId.Value)));
        await _context.SaveChangesAsync();

        _context.Bookings.RemoveRange(_context.Bookings.Where(b => carIds.Contains(b.CarId)));
        await _context.SaveChangesAsync();

        _context.CarPhotos.RemoveRange(_context.CarPhotos.Where(p => carIds.Contains(p.CarId)));
        _context.CarPriceOffers.RemoveRange(_context.CarPriceOffers.Where(o => carIds.Contains(o.CarId)));
        _context.CarAvailabilityBlocks.RemoveRange(_context.CarAvailabilityBlocks.Where(b => carIds.Contains(b.CarId)));
        _context.CarViews.RemoveRange(_context.CarViews.Where(v => carIds.Contains(v.CarId)));
        _context.Favorites.RemoveRange(_context.Favorites.Where(f => carIds.Contains(f.CarId)));
        await _context.SaveChangesAsync();

        _context.Cars.RemoveRange(_context.Cars.Where(c => c.CompanyId == id));
        await _context.SaveChangesAsync();

        _context.CompanyVerifications.RemoveRange(_context.CompanyVerifications.Where(v => v.CompanyId == id));
        _context.CompanySubscriptions.RemoveRange(_context.CompanySubscriptions.Where(s => s.CompanyId == id));
        _context.AmenitySuggestions.RemoveRange(_context.AmenitySuggestions.Where(a => a.CompanyId == id));
        await _context.SaveChangesAsync();

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(new { message = "Biznesi u fshi plotesisht." });
    }

    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPendingCompanies()
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        var pending = await _context.Companies
            .Where(c => c.EshteVerifikuar == false)
            .Select(c => new
            {
                c.CompanyId,
                c.Emri,
                c.Email,
                c.Telefoni,
                c.Qyteti,
                c.Nipt,
                CertifikataUrl = _context.CompanyVerifications
                    .Where(v => v.CompanyId == c.CompanyId)
                    .OrderByDescending(v => v.DataDorezimit)
                    .Select(v => v.CertifikataUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(pending);
    }
}