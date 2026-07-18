using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CreateCarDto(
    int CompanyId, string Marka, string Modeli, int Viti, int Km,
    string Karburanti, string Transmisioni, string? Ngjyra, string Targa,
    string Kategoria, int NumriVendeve, bool Klimatizimi, decimal CmimiDites,
    string? Pershkrimi = null, int? Kubatura = null, int? Cilindra = null);

[ApiController]
[Route("api/[controller]")]
public class CarsController : ControllerBase
{
    private readonly ERentalDbContext _context;

    public CarsController(ERentalDbContext context)
    {
        _context = context;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetCars()
    {
        var cars = await _context.Cars
            .Include(c => c.CarPhotos)
            .Include(c => c.Company)
            .ToListAsync();
        return Ok(cars);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCarById(int id)
    {
        var car = await _context.Cars
            .Include(c => c.CarPhotos)
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.CarId == id);

        if (car == null) return NotFound();

        try
        {
            int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : null;
            _context.CarViews.Add(new CarView
            {
                CarId = id,
                UserId = userId,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();
        }
        catch { }

        return Ok(car);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateCar(CreateCarDto dto)
    {
        var userId = GetUserId();

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == dto.CompanyId);
        if (company == null) return BadRequest("Biznesi nuk ekziston.");

        if (company.OwnerUserId != userId)
            return Forbid();

        var car = new Car
        {
            CompanyId = dto.CompanyId,
            Marka = dto.Marka,
            Modeli = dto.Modeli,
            Viti = dto.Viti,
            Km = dto.Km,
            Karburanti = dto.Karburanti,
            Transmisioni = dto.Transmisioni,
            Ngjyra = dto.Ngjyra,
            Targa = dto.Targa,
            Kategoria = dto.Kategoria,
            NumriVendeve = dto.NumriVendeve,
            Klimatizimi = dto.Klimatizimi,
            CmimiDites = dto.CmimiDites,
            Pershkrimi = dto.Pershkrimi,
            Kubatura = dto.Kubatura,
            Cilindra = dto.Cilindra,
            Statusi = "active"
        };

        _context.Cars.Add(car);
        await _context.SaveChangesAsync();

        return Ok(car);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateCar(int id, CreateCarDto dto)
    {
        var userId = GetUserId();

        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();

        if (car.Company.OwnerUserId != userId)
            return Forbid();

        car.Marka = dto.Marka;
        car.Modeli = dto.Modeli;
        car.Viti = dto.Viti;
        car.Km = dto.Km;
        car.Karburanti = dto.Karburanti;
        car.Transmisioni = dto.Transmisioni;
        car.Ngjyra = dto.Ngjyra;
        car.Targa = dto.Targa;
        car.Kategoria = dto.Kategoria;
        car.NumriVendeve = dto.NumriVendeve;
        car.Klimatizimi = dto.Klimatizimi;
        car.CmimiDites = dto.CmimiDites;
        car.Pershkrimi = dto.Pershkrimi;
        car.Kubatura = dto.Kubatura;
        car.Cilindra = dto.Cilindra;

        await _context.SaveChangesAsync();
        return Ok(car);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableCars(DateOnly dataFillimit, DateOnly dataPerfundimit)
    {
        var carsIds = await _context.Cars
            .Where(c => c.Statusi == "active")
            .Where(c => !c.CarAvailabilityBlocks.Any(b =>
                b.DataFillimit <= dataPerfundimit && b.DataPerfundimit >= dataFillimit))
            .Select(c => c.CarId)
            .ToListAsync();

        var cars = await _context.Cars
            .Where(c => carsIds.Contains(c.CarId))
            .Include(c => c.CarPhotos)
            .Include(c => c.Company)
            .ToListAsync();

        return Ok(cars);
    }
}