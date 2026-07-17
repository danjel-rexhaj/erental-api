using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace ERental.Controllers;

public record AddCarPhotoDto(int CarId, string UrlFotos, bool EshteKryesore, string? Kategoria = null);

[ApiController]
[Route("api/[controller]")]
public class CarPhotosController : ControllerBase
{
    private readonly ERentalDbContext _context;

    public CarPhotosController(ERentalDbContext context)
    {
        _context = context;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddPhoto(AddCarPhotoDto dto)
    {
        var userId = GetUserId();

        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == dto.CarId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        if (car.Company.OwnerUserId != userId)
            return Forbid();

        if (await _context.CarPhotos.CountAsync(p => p.CarId == dto.CarId) >= 7)
            return BadRequest("Maksimumi 7 foto per makine.");

        // Nese eshte kryesore, hiq flag-un nga fotot e tjera te se njejtes makine
        if (dto.EshteKryesore)
        {
            var fotoEkzistuese = await _context.CarPhotos.Where(p => p.CarId == dto.CarId).ToListAsync();
            foreach (var f in fotoEkzistuese) f.EshteKryesore = false;
        }

        var photo = new CarPhoto
        {
            CarId = dto.CarId,
            UrlFotos = dto.UrlFotos,
            EshteKryesore = dto.EshteKryesore,
            Kategoria = dto.Kategoria
        };

        _context.CarPhotos.Add(photo);
        await _context.SaveChangesAsync();

        return Ok(photo);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeletePhoto(int id)
    {
        var userId = GetUserId();

        var photo = await _context.CarPhotos.Include(p => p.Car).ThenInclude(c => c.Company)
            .FirstOrDefaultAsync(p => p.PhotoId == id);
        if (photo == null) return NotFound();

        if (photo.Car.Company.OwnerUserId != userId)
            return Forbid();

        _context.CarPhotos.Remove(photo);
        await _context.SaveChangesAsync();

        return Ok(new { deleted = true });
    }

    [HttpGet("car/{carId}")]
    public async Task<IActionResult> GetPhotosForCar(int carId)
    {
        var photos = await _context.CarPhotos.Where(p => p.CarId == carId).ToListAsync();
        return Ok(photos);
    }



    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> UploadPhoto(IFormFile file, [FromForm] int carId, [FromForm] bool eshteKryesore, [FromForm] string? kategoria = null)
    {
        var userId = GetUserId();

        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == carId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        if (car.Company.OwnerUserId != userId)
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Nuk u dergua asnje file.");

        if (await _context.CarPhotos.CountAsync(p => p.CarId == carId) >= 7)
            return BadRequest("Maksimumi 7 foto per makine.");

        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var accessKey = config["R2:AccessKey"];
        var secretKey = config["R2:SecretKey"];
        var endpoint = config["R2:Endpoint"];
        var bucketName = config["R2:BucketName"];
        var publicUrl = config["R2:PublicUrl"];

        var s3Config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
        };

        using var s3Client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);

        var fileName = $"cars/{carId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        using var stream = file.OpenReadStream();
        var putRequest = new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = bucketName,
            Key = fileName,
            InputStream = stream,
            ContentType = file.ContentType,
            DisablePayloadSigning = true,
            UseChunkEncoding = false
        };

        await s3Client.PutObjectAsync(putRequest);

        var fullUrl = $"{publicUrl}/{fileName}";

        if (eshteKryesore)
        {
            var fotoEkzistuese = await _context.CarPhotos.Where(p => p.CarId == carId).ToListAsync();
            foreach (var f in fotoEkzistuese) f.EshteKryesore = false;
        }

        var photo = new CarPhoto
        {
            CarId = carId,
            UrlFotos = fullUrl,
            EshteKryesore = eshteKryesore,
            Kategoria = kategoria
        };

        _context.CarPhotos.Add(photo);
        await _context.SaveChangesAsync();

        return Ok(photo);
    }
}