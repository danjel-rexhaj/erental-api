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

    private record SeriesPoint(int Year, int Month, int? Day, int Count);

    private static List<SeriesPoint> BuildSeries(List<DateTime> dates, bool daily)
    {
        if (daily)
            return dates.GroupBy(d => d.Date)
                .Select(g => new SeriesPoint(g.Key.Year, g.Key.Month, g.Key.Day, g.Count()))
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
                .ToList();

        return dates.GroupBy(d => new { d.Year, d.Month })
            .Select(g => new SeriesPoint(g.Key.Year, g.Key.Month, null, g.Count()))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();
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
        var carDates = await _context.Cars.Where(c => c.DataKrijimit >= since).Select(c => c.DataKrijimit!.Value).ToListAsync();
        var bookingDates = await _context.Bookings.Where(b => b.DataKrijimit >= since).Select(b => b.DataKrijimit!.Value).ToListAsync();
        var verificationDates = await _context.CompanyVerifications.Where(v => v.DataDorezimit >= since).Select(v => v.DataDorezimit!.Value).ToListAsync();

        var series = new
        {
            users = BuildSeries(userDates, daily),
            companies = BuildSeries(companyDates, daily),
            cars = BuildSeries(carDates, daily),
            bookings = BuildSeries(bookingDates, daily),
            verifications = BuildSeries(verificationDates, daily)
        };

        var completedPayments = await _context.Payments
            .Where(p => p.Statusi == "completed")
            .ToListAsync();

        var totals = new
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalCompanies = await _context.Companies.CountAsync(),
            TotalCars = await _context.Cars.CountAsync(),
            TotalBookings = await _context.Bookings.CountAsync(),
            PendingVerifications = await _context.Companies.CountAsync(c => c.EshteVerifikuar == false),
            TotalPlatformRevenue = completedPayments.Sum(p => p.ShumaTotale),
            TotalPlatformProfit = completedPayments.Sum(p => p.Komisioni)
        };

        var topCompanies = await _context.Bookings
            .GroupBy(b => new { b.Car.CompanyId, b.Car.Company.Emri })
            .Select(g => new { g.Key.Emri, Rezervime = g.Count() })
            .OrderByDescending(x => x.Rezervime)
            .Take(5)
            .ToListAsync();

        var companyBreakdown = await _context.Payments
            .Where(p => p.Statusi == "completed")
            .Join(_context.Bookings, p => p.BookingId, b => b.BookingId, (p, b) => new { p.ShumaTotale, p.Komisioni, b.CarId })
            .Join(_context.Cars, x => x.CarId, c => c.CarId, (x, c) => new { x.ShumaTotale, x.Komisioni, c.CompanyId })
            .Join(_context.Companies, x => x.CompanyId, co => co.CompanyId, (x, co) => new { x.ShumaTotale, x.Komisioni, co.Emri })
            .GroupBy(x => x.Emri)
            .Select(g => new { Emri = g.Key, TeArdhura = g.Sum(x => x.ShumaTotale), Fitimi = g.Sum(x => x.Komisioni) })
            .OrderByDescending(x => x.Fitimi)
            .ToListAsync();

        return Ok(new { series, totals, topCompanies, companyBreakdown });
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
