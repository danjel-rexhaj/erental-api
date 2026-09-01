using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CreateReviewDto(int BookingId, int Rating, string? Koment);

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    public ReviewsController(ERentalDbContext context) => _context = context;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview(CreateReviewDto dto)
    {
        var userId = GetUserId();

        var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);
        if (booking == null) return NotFound("Rezervimi nuk u gjet.");

        if (booking.UserId != userId) return Forbid();
        if (booking.Statusi != "completed") return BadRequest("Vleresimi lejohet vetem per rezervime te perfunduara.");
        if (dto.Rating < 1 || dto.Rating > 5) return BadRequest("Vleresimi duhet te jete nga 1 deri ne 5.");

        if (await _context.Reviews.AnyAsync(r => r.BookingId == dto.BookingId))
            return BadRequest("Ky rezervim eshte vleresuar tashme.");

        var review = new Review
        {
            UserId = userId,
            CompanyId = booking.Car.CompanyId,
            BookingId = dto.BookingId,
            Rating = dto.Rating,
            Koment = dto.Koment
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return Ok(review);
    }

    // Cross-company feed for the homepage "recent reviews" section -- only reviews that actually
    // have both a rating and real comment text (a bare star rating with no words reads as filler
    // there), from currently-verified companies.
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent(int take = 10)
    {
        take = Math.Clamp(take, 1, 20);

        var reviews = await _context.Reviews
            .Include(r => r.Company)
            .Include(r => r.User)
            .Where(r => r.Rating != null && r.Koment != null && r.Koment != "" && r.Company.EshteVerifikuar == true)
            .OrderByDescending(r => r.Data)
            .Take(take)
            .Select(r => new
            {
                r.ReviewId,
                r.Rating,
                r.Koment,
                r.Data,
                CompanyEmri = r.Company.Emri,
                Emri = r.User.Emri,
                MbiemriInicial = r.User.Mbiemri.Substring(0, 1)
            })
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetCompanyReviews(int companyId)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.Data)
            .Select(r => new
            {
                r.ReviewId,
                r.Rating,
                r.Koment,
                r.Data,
                Emri = r.User.Emri,
                Mbiemri = r.User.Mbiemri
            })
            .ToListAsync();

        return Ok(reviews);
    }
}
