using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    public AnalyticsController(ERentalDbContext context) => _context = context;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("business")]
    [Authorize]
    public async Task<IActionResult> GetBusinessAnalytics(int months = 6, int? days = null, int? companyId = null)
    {
        var userId = GetUserId();

        Infrastructure.Entities.Company? company;
        if (companyId.HasValue && userId == 1)
            company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId.Value);
        else
            company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == userId);

        if (company == null) return NotFound("Nuk ke asnje biznes te regjistruar.");

        var carIds = await _context.Cars.Where(c => c.CompanyId == company.CompanyId).Select(c => c.CarId).ToListAsync();

        var viewsPerCar = await _context.CarViews
            .Where(v => carIds.Contains(v.CarId))
            .GroupBy(v => new { v.CarId, v.Car.Marka, v.Car.Modeli })
            .Select(g => new { g.Key.CarId, Makina = g.Key.Marka + " " + g.Key.Modeli, Shikime = g.Count() })
            .OrderByDescending(x => x.Shikime)
            .ToListAsync();

        bool daily = days.HasValue;
        var since = daily
            ? DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-Math.Clamp(days!.Value, 1, 90)), DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(DateTime.UtcNow.AddMonths(-Math.Clamp(months, 1, 24)), DateTimeKind.Unspecified);

        var paymentsData = await _context.Payments
            .Where(p => p.Statusi == "completed")
            .Join(_context.Bookings, p => p.BookingId, b => b.BookingId, (p, b) => new { p.ShumaBiznesit, p.DataPageses, b.CarId })
            .Where(x => carIds.Contains(x.CarId) && x.DataPageses >= since)
            .ToListAsync();

        var monthly = daily
            ? paymentsData.GroupBy(x => x.DataPageses!.Value.Date)
                .Select(g => new { g.Key.Year, g.Key.Month, Day = (int?)g.Key.Day, Rezervime = g.Count(), TeArdhura = g.Sum(x => x.ShumaBiznesit) })
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
                .ToList()
            : paymentsData.GroupBy(x => new { x.DataPageses!.Value.Year, x.DataPageses!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Day = (int?)null, Rezervime = g.Count(), TeArdhura = g.Sum(x => x.ShumaBiznesit) })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

        var totalRevenue = paymentsData.Sum(x => x.ShumaBiznesit);
        var totalBookings = await _context.Bookings.CountAsync(b => carIds.Contains(b.CarId));
        var totalViews = viewsPerCar.Sum(x => x.Shikime);

        return Ok(new
        {
            companyId = company.CompanyId,
            companyName = company.Emri,
            viewsPerCar,
            monthly,
            totals = new { totalRevenue, totalBookings, totalViews }
        });
    }

    [HttpGet("admin")]
    [Authorize]
    public async Task<IActionResult> GetAdminAnalytics(int months = 6, int? days = null)
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        bool daily = days.HasValue;
        var since = daily
            ? DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-Math.Clamp(days!.Value, 1, 90)), DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(DateTime.UtcNow.AddMonths(-Math.Clamp(months, 1, 24)), DateTimeKind.Unspecified);

        var userDates = await _context.Users.Where(u => u.DataRegjistrimit >= since).Select(u => u.DataRegjistrimit!.Value).ToListAsync();
        var companyDates = await _context.Companies.Where(c => c.DataRegjistrimit >= since).Select(c => c.DataRegjistrimit!.Value).ToListAsync();

        (int Year, int Month, int? Day) Key(DateTime d) => daily ? (d.Year, d.Month, d.Day) : (d.Year, d.Month, (int?)null);

        var monthlyUsers = userDates.GroupBy(Key)
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() }).ToList();
        var monthlyCompanies = companyDates.GroupBy(Key)
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() }).ToList();

        var monthKeys = monthlyUsers.Select(x => (x.Year, x.Month, x.Day))
            .Union(monthlyCompanies.Select(x => (x.Year, x.Month, x.Day)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
            .ToList();

        var monthly = monthKeys.Select(m => new
        {
            m.Year,
            m.Month,
            m.Day,
            Users = monthlyUsers.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month && x.Day == m.Day)?.Count ?? 0,
            Companies = monthlyCompanies.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month && x.Day == m.Day)?.Count ?? 0
        }).ToList();

        var totals = new
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalCompanies = await _context.Companies.CountAsync(),
            TotalCars = await _context.Cars.CountAsync(),
            TotalBookings = await _context.Bookings.CountAsync(),
            PendingVerifications = await _context.Companies.CountAsync(c => c.EshteVerifikuar == false)
        };

        var topCompanies = await _context.Bookings
            .GroupBy(b => new { b.Car.CompanyId, b.Car.Company.Emri })
            .Select(g => new { g.Key.Emri, Rezervime = g.Count() })
            .OrderByDescending(x => x.Rezervime)
            .Take(5)
            .ToListAsync();

        return Ok(new { monthly, totals, topCompanies });
    }

    [HttpGet("admin/logins")]
    [Authorize]
    public async Task<IActionResult> GetLoginLogs(int page = 1, int pageSize = 50)
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        var query = _context.LoginLogs.OrderByDescending(l => l.DataHyrjes);
        var total = await query.CountAsync();
        var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var since24h = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-24), DateTimeKind.Unspecified);
        var failedLast24h = await _context.LoginLogs.CountAsync(l => l.Sukses == false && l.DataHyrjes >= since24h);

        return Ok(new { logs, total, page, pageSize, failedLast24h });
    }
}
