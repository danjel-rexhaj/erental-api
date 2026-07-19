using ERental.Application.Interfaces;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record RegisterCompanyDto(string Emri, string Email, string Telefoni, string Adresa, string Qyteti, string Nipt);
public record UpdateLocationDto(double Latitude, double Longitude);

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IFileUploadService _fileUploadService;

    public CompaniesController(ERentalDbContext context, IFileUploadService fileUploadService)
    {
        _context = context;
        _fileUploadService = fileUploadService;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> RegisterCompany(
    [FromForm] string emri, [FromForm] string telefoni, [FromForm] string adresa,
    [FromForm] string qyteti, [FromForm] string nipt, [FromForm] double? latitude,
    [FromForm] double? longitude, [FromForm] bool allowCashPayment, IFormFile? certifikataFile)
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
            AllowCashPayment = allowCashPayment,
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

    [HttpPut("my-company/cash-payment")]
    [Authorize]
    public async Task<IActionResult> UpdateCashPayment([FromBody] bool allow)
    {
        var userId = GetUserId();
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);
        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        company.AllowCashPayment = allow;
        await _context.SaveChangesAsync();

        return Ok(new { company.AllowCashPayment });
    }

    [HttpPut("{id}/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyCompany(int id)
    {
        var userId = GetUserId();

        if (userId != 1)
            return Forbid();

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company == null) return NotFound();

        company.EshteVerifikuar = true;
        company.DataVerifikimit = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        company.Statusi = "active";

        await _context.SaveChangesAsync();

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