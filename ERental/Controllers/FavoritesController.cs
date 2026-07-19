using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly ERentalDbContext _context;
    public FavoritesController(ERentalDbContext context) => _context = context;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("ids")]
    public async Task<IActionResult> GetFavoriteIds()
    {
        var userId = GetUserId();
        var ids = await _context.Favorites.Where(f => f.UserId == userId).Select(f => f.CarId).ToListAsync();
        return Ok(ids);
    }

    [HttpGet]
    public async Task<IActionResult> GetFavoriteCars()
    {
        var userId = GetUserId();
        var cars = await _context.Favorites
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.DataKrijimit)
            .Select(f => f.Car)
            .Include(c => c.CarPhotos)
            .Include(c => c.Company)
            .ToListAsync();
        return Ok(cars);
    }

    [HttpPost("{carId}")]
    public async Task<IActionResult> AddFavorite(int carId)
    {
        var userId = GetUserId();
        var carExists = await _context.Cars.AnyAsync(c => c.CarId == carId);
        if (!carExists) return NotFound("Makina nuk ekziston.");

        var already = await _context.Favorites.AnyAsync(f => f.UserId == userId && f.CarId == carId);
        if (!already)
        {
            _context.Favorites.Add(new Favorite { UserId = userId, CarId = carId });
            await _context.SaveChangesAsync();
        }

        return Ok(new { favorited = true });
    }

    [HttpDelete("{carId}")]
    public async Task<IActionResult> RemoveFavorite(int carId)
    {
        var userId = GetUserId();
        var favorite = await _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.CarId == carId);
        if (favorite != null)
        {
            _context.Favorites.Remove(favorite);
            await _context.SaveChangesAsync();
        }

        return Ok(new { favorited = false });
    }
}
