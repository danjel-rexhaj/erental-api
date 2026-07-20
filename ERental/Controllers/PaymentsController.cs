using ERental.Application.Interfaces;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CapturePaymentDto(int CarId, DateOnly DataFillimit, DateOnly DataPerfundimit, string Method, string PaypalOrderId);
public record CreateOrderDto(int CarId, DateOnly DataFillimit, DateOnly DataPerfundimit, string Method, string? ReturnUrl = null, string? CancelUrl = null);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IPayPalService _payPal;

    public PaymentsController(ERentalDbContext context, IPayPalService payPal)
    {
        _context = context;
        _payPal = payPal;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Creates the PayPal order server-side (amount computed from the car/dates, never trusting the
    // client). When returnUrl/cancelUrl are given, PayPal's redirect-based checkout is used — the
    // frontend sends the browser to the returned approveUrl instead of rendering an inline lightbox.
    [HttpPost("paypal/create-order")]
    [Authorize]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        if (dto.Method != "deposit" && dto.Method != "full")
            return BadRequest("Menyre pagese e panjohur.");

        var user = await _context.Users.FindAsync(GetUserId());
        if (user == null || string.IsNullOrWhiteSpace(user.PatentaFotoPara) || string.IsNullOrWhiteSpace(user.PatentaFotoMbrapa))
            return BadRequest("Duhet te shtosh foton e patentes (para dhe mbrapa) ne profilin tend para se te rezervosh.");

        var car = await _context.Cars.FindAsync(dto.CarId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        if (dto.DataPerfundimit <= dto.DataFillimit)
            return BadRequest("Datat nuk jane te vlefshme.");

        int dite = dto.DataPerfundimit.DayNumber - dto.DataFillimit.DayNumber;
        decimal totali = dite * car.CmimiDites;
        decimal shuma = dto.Method == "deposit" ? car.CmimiDites : totali;

        var result = await _payPal.CreateOrderAsync(shuma, "EUR", dto.ReturnUrl, dto.CancelUrl);
        if (!result.Success)
            return BadRequest(result.Error ?? "Krijimi i pageses deshtoi.");

        return Ok(new { orderId = result.OrderId, approveUrl = result.ApproveUrl });
    }

    // Captures a PayPal order the client already approved, verifying server-side that the captured
    // amount matches what this car/date-range/method actually costs before handing the capture id
    // back for use in POST /Bookings. Prevents a tampered client-side order amount from being accepted.
    [HttpPost("paypal/capture")]
    [Authorize]
    public async Task<IActionResult> CapturePayment(CapturePaymentDto dto)
    {
        if (dto.Method != "deposit" && dto.Method != "full")
            return BadRequest("Menyre pagese e panjohur.");

        var car = await _context.Cars.FindAsync(dto.CarId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        if (dto.DataPerfundimit <= dto.DataFillimit)
            return BadRequest("Datat nuk jane te vlefshme.");

        int dite = dto.DataPerfundimit.DayNumber - dto.DataFillimit.DayNumber;
        decimal totali = dite * car.CmimiDites;
        decimal pritshme = dto.Method == "deposit" ? car.CmimiDites : totali;

        var result = await _payPal.CaptureOrderAsync(dto.PaypalOrderId);
        if (!result.Success)
            return BadRequest(result.Error ?? "Pagesa nuk u pranua nga PayPal.");

        if (result.Amount == null || Math.Abs(result.Amount.Value - pritshme) > 0.01m)
        {
            if (result.CaptureId != null)
                await _payPal.RefundCaptureAsync(result.CaptureId, result.Amount ?? pritshme, result.Currency ?? "EUR");
            return BadRequest("Shuma e paguar nuk perputhet me cmimin e pritshem. Pagesa u rimbursua automatikisht.");
        }

        return Ok(new { captureId = result.CaptureId, amountPaid = result.Amount });
    }

    // Transaction ledger for the caller's own business — reference number, amounts, and who/what it was for.
    [HttpGet("my-company")]
    [Authorize]
    public async Task<IActionResult> GetMyCompanyPayments()
    {
        var userId = GetUserId();

        var payments = await _context.Payments
            .Include(p => p.Booking).ThenInclude(b => b.Car).ThenInclude(c => c.Company)
            .Include(p => p.Booking).ThenInclude(b => b.User)
            .Where(p => p.Booking.Car.Company.OwnerUserId == userId)
            .OrderByDescending(p => p.DataPageses)
            .Select(p => new
            {
                p.PaymentId,
                p.DataPageses,
                p.Statusi,
                p.MetodaPageses,
                p.ShumaTotale,
                p.ShumaPaguarOnline,
                p.Komisioni,
                p.ShumaBiznesit,
                p.PaypalCaptureId,
                Booking = new { p.Booking.BookingId, p.Booking.DataFillimit, p.Booking.DataPerfundimit },
                Car = new { p.Booking.Car.Marka, p.Booking.Car.Modeli },
                Klienti = new { p.Booking.User.Emri, p.Booking.User.Mbiemri }
            })
            .ToListAsync();

        return Ok(payments);
    }

    // Platform-wide transaction ledger, every business — admin only.
    [HttpGet("admin")]
    [Authorize]
    public async Task<IActionResult> GetAllPayments()
    {
        var userId = GetUserId();
        if (userId != 1) return Forbid();

        var payments = await _context.Payments
            .Include(p => p.Booking).ThenInclude(b => b.Car).ThenInclude(c => c.Company)
            .Include(p => p.Booking).ThenInclude(b => b.User)
            .OrderByDescending(p => p.DataPageses)
            .Select(p => new
            {
                p.PaymentId,
                p.DataPageses,
                p.Statusi,
                p.MetodaPageses,
                p.ShumaTotale,
                p.ShumaPaguarOnline,
                p.Komisioni,
                p.ShumaBiznesit,
                p.PaypalCaptureId,
                Booking = new { p.Booking.BookingId, p.Booking.DataFillimit, p.Booking.DataPerfundimit },
                Car = new { p.Booking.Car.Marka, p.Booking.Car.Modeli },
                Klienti = new { p.Booking.User.Emri, p.Booking.User.Mbiemri },
                Biznesi = new { p.Booking.Car.Company.CompanyId, p.Booking.Car.Company.Emri }
            })
            .ToListAsync();

        return Ok(payments);
    }
}
