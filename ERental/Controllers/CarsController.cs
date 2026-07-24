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
    string? Pershkrimi = null, int? Kubatura = null, int? Cilindra = null, string[]? Amenities = null);

public record CreateBlockDto(DateOnly DataFillimit, DateOnly DataPerfundimit, string? Shenim);
public record AdminUpdateCarDto(decimal? CmimiDites, string? Statusi);
public record UpdateCarStatusDto(string Statusi);

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

    // Attaches average rating / review count / active car count onto each car's Company navigation,
    // computed via grouped queries instead of Includes so we never ship every review/car back down.
    private async Task AttachCompanyStatsAsync(IEnumerable<Car> cars)
    {
        var companyIds = cars.Select(c => c.CompanyId).Distinct().ToList();
        if (companyIds.Count == 0) return;

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

        foreach (var car in cars)
        {
            if (car.Company == null) continue;
            if (ratingStats.TryGetValue(car.CompanyId, out var rs))
            {
                car.Company.AvgRating = Math.Round(rs.Avg, 1);
                car.Company.ReviewCount = rs.Count;
            }
            car.Company.CarCount = carCounts.TryGetValue(car.CompanyId, out var cc) ? cc : 0;
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCars()
    {
        var cars = await _context.Cars
            .Include(c => c.CarPhotos)
            .Include(c => c.Company)
            .ToListAsync();
        await AttachCompanyStatsAsync(cars);
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
        await AttachCompanyStatsAsync(new[] { car });
        return Ok(car);
    }

    // Called explicitly by the frontend when a car's detail page actually opens
    // (the detail page reuses the car object already fetched via the search-results
    // list, so GetCarById above is never hit in the real browsing flow).
    [HttpPost("{id}/view")]
    public async Task<IActionResult> LogView(int id)
    {
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

        return Ok();
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
            Amenities = dto.Amenities,
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

        if (car.Company.OwnerUserId != userId && userId != 1)
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
        car.Amenities = dto.Amenities;

        await _context.SaveChangesAsync();
        return Ok(car);
    }

    [HttpPut("{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateCarStatus(int id, UpdateCarStatusDto dto)
    {
        var userId = GetUserId();
        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();
        if (car.Company.OwnerUserId != userId && userId != 1) return Forbid();
        if (dto.Statusi != "active" && dto.Statusi != "inactive") return BadRequest("Statusi i pavlefshem.");

        car.Statusi = dto.Statusi;
        await _context.SaveChangesAsync();
        return Ok(new { car.CarId, car.Statusi });
    }

    // Cars with any booking history (even cancelled) can't be hard-deleted -- deleting would either
    // violate the bookings FK or orphan historical bookings/invoices. Deactivate those instead.
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteCar(int id)
    {
        var userId = GetUserId();
        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();
        if (car.Company.OwnerUserId != userId && userId != 1) return Forbid();

        if (await _context.Bookings.AnyAsync(b => b.CarId == id))
            return BadRequest("Kjo makine ka rezervime ne histori dhe nuk mund te fshihet perfundimisht -- caktivizoje ne vend te kesaj.");

        _context.CarPhotos.RemoveRange(_context.CarPhotos.Where(p => p.CarId == id));
        _context.CarAvailabilityBlocks.RemoveRange(_context.CarAvailabilityBlocks.Where(b => b.CarId == id));
        _context.CarViews.RemoveRange(_context.CarViews.Where(v => v.CarId == id));
        _context.Cars.Remove(car);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Makina u fshi." });
    }

    [HttpPut("{id}/admin")]
    [Authorize]
    public async Task<IActionResult> AdminUpdateCar(int id, AdminUpdateCarDto dto)
    {
        if (GetUserId() != 1) return Forbid();

        var car = await _context.Cars.FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();

        if (dto.CmimiDites.HasValue) car.CmimiDites = dto.CmimiDites.Value;
        if (!string.IsNullOrWhiteSpace(dto.Statusi)) car.Statusi = dto.Statusi;
        await _context.SaveChangesAsync();

        return Ok(car);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableCars(DateOnly dataFillimit, DateOnly dataPerfundimit)
    {
        if (dataPerfundimit <= dataFillimit)
            return BadRequest("Data e dorezimit duhet te jete pas dates se marrjes.");

        // Boundaries touching (e.g. one rental ends the 23rd, the next starts the 23rd) are not a conflict —
        // same-day turnover is allowed. Only a genuine overlap blocks availability.
        var activeCars = await _context.Cars
            .Where(c => c.Statusi == "active")
            .Include(c => c.CarPhotos)
            .Include(c => c.Company)
            .Include(c => c.CarAvailabilityBlocks)
            .ToListAsync();

        var nearMissThreshold = dataFillimit.AddDays(3);
        var result = new List<Car>();
        foreach (var c in activeCars)
        {
            var konfliktet = c.CarAvailabilityBlocks
                .Where(b => b.DataFillimit < dataPerfundimit && b.DataPerfundimit > dataFillimit)
                .ToList();

            if (konfliktet.Count == 0)
            {
                c.EshteELire = true;
                result.Add(c);
            }
            else
            {
                var lirohetMe = konfliktet.Max(b => b.DataPerfundimit);
                if (lirohetMe <= nearMissThreshold)
                {
                    c.EshteELire = false;
                    c.LirohetMe = lirohetMe;
                    result.Add(c);
                }
            }
        }

        await AttachCompanyStatsAsync(result);
        return Ok(result.OrderByDescending(c => c.EshteELire));
    }

    [HttpGet("{id}/availability")]
    public async Task<IActionResult> GetCarAvailability(int id)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var blocks = await _context.CarAvailabilityBlocks
            .Where(b => b.CarId == id && b.DataPerfundimit >= today)
            .OrderBy(b => b.DataFillimit)
            .Select(b => new { b.DataFillimit, b.DataPerfundimit })
            .ToListAsync();

        return Ok(blocks);
    }

    // Lets a business block off dates for a car it owns to cover rentals arranged outside the
    // platform (walk-ins, phone bookings) so those days don't get double-booked here too.
    [HttpGet("{id}/blocks")]
    [Authorize]
    public async Task<IActionResult> GetBlocks(int id)
    {
        var userId = GetUserId();
        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();
        if (car.Company.OwnerUserId != userId) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var blocks = await _context.CarAvailabilityBlocks
            .Where(b => b.CarId == id && b.DataPerfundimit >= today)
            .OrderBy(b => b.DataFillimit)
            .Select(b => new
            {
                b.BlockId,
                b.DataFillimit,
                b.DataPerfundimit,
                b.Shenim,
                EshteRezervimPlatforme = b.Shenim != null && b.Shenim.StartsWith("Booking #")
            })
            .ToListAsync();

        return Ok(blocks);
    }

    [HttpPost("{id}/blocks")]
    [Authorize]
    public async Task<IActionResult> CreateBlock(int id, CreateBlockDto dto)
    {
        var userId = GetUserId();
        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();
        if (car.Company.OwnerUserId != userId) return Forbid();

        if (dto.DataPerfundimit <= dto.DataFillimit)
            return BadRequest("Data e perfundimit duhet te jete pas dates se fillimit.");

        var konflikt = await _context.CarAvailabilityBlocks
            .AnyAsync(b => b.CarId == id && b.DataFillimit < dto.DataPerfundimit && b.DataPerfundimit > dto.DataFillimit);
        if (konflikt)
            return BadRequest("Makina eshte tashme e zene per pjese te ketyre datave.");

        var block = new CarAvailabilityBlock
        {
            CarId = id,
            DataFillimit = dto.DataFillimit,
            DataPerfundimit = dto.DataPerfundimit,
            Arsyeja = "manual",
            Shenim = string.IsNullOrWhiteSpace(dto.Shenim) ? "Rezervim jashte platformes" : dto.Shenim.Trim()
        };

        _context.CarAvailabilityBlocks.Add(block);
        await _context.SaveChangesAsync();

        return Ok(block);
    }

    [HttpDelete("{id}/blocks/{blockId}")]
    [Authorize]
    public async Task<IActionResult> DeleteBlock(int id, int blockId)
    {
        var userId = GetUserId();
        var car = await _context.Cars.Include(c => c.Company).FirstOrDefaultAsync(c => c.CarId == id);
        if (car == null) return NotFound();
        if (car.Company.OwnerUserId != userId) return Forbid();

        var block = await _context.CarAvailabilityBlocks.FirstOrDefaultAsync(b => b.BlockId == blockId && b.CarId == id);
        if (block == null) return NotFound();

        if (block.Shenim != null && block.Shenim.StartsWith("Booking #"))
            return BadRequest("Ky bllokim eshte nje rezervim nga platforma dhe nuk mund te fshihet ketu.");

        _context.CarAvailabilityBlocks.Remove(block);
        await _context.SaveChangesAsync();

        return Ok();
    }
}