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

public record RegisterCompanyDto(string Emri, string Email, string Telefoni, string Adresa, string Qyteti, string Nipt);
public record UpdateLocationDto(double Latitude, double Longitude);
public record UpdateCompanyDto(string Emri, string? Telefoni, string? Adresa, string? Qyteti);
public record AdminUpdateCompanyDto(string Emri, string? Telefoni, string? Adresa, string? Qyteti, string? Statusi);

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IFileUploadService _fileUploadService;
    private readonly IEmailService _emailService;
    private readonly IHubContext<NotificationHub> _hub;

    public CompaniesController(ERentalDbContext context, IFileUploadService fileUploadService, IEmailService emailService, IHubContext<NotificationHub> hub)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _emailService = emailService;
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

    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> RegisterCompany(
    [FromForm] string emri, [FromForm] string telefoni, [FromForm] string adresa,
    [FromForm] string qyteti, [FromForm] string nipt, [FromForm] double? latitude,
    [FromForm] double? longitude, IFormFile? certifikataFile)
    {
        var userId = GetUserId();

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
            Latitude = latitude,
            Longitude = longitude,
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

            try
            {
                await NotifyAsync(1, "Kerkese verifikimi biznesi", $"{company.Emri} dergoi certifikaten e NIPT-it dhe pret verifikim.", "admin_company_verification");

                var admin = await _context.Users.FindAsync(1);
                if (admin?.Email != null)
                    await _emailService.SendAdminVerificationRequestAsync(admin.Email, company.Emri, company.CompanyId);
            }
            catch (Exception ex) { Console.WriteLine($"Admin verification email error: {ex.Message}"); }
        }

        return Ok(new { company.CompanyId, company.Emri, company.Nipt, Statusi = "Pending verifikim" });
    }

    [HttpGet]
    public async Task<IActionResult> GetCompanies()
    {
        var companies = await _context.Companies.ToListAsync();
        return Ok(companies);
    }

    [HttpGet("my-company")]
    [Authorize]
    public async Task<IActionResult> GetMyCompany()
    {
        var userId = GetUserId();
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");
        return Ok(company);
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
        await _context.SaveChangesAsync();

        return Ok(company);
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

        return Ok(company);
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