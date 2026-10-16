using ERental.Services;
﻿using ERental.Application.Interfaces;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CapturePaymentDto(int CarId, DateOnly DataFillimit, DateOnly DataPerfundimit, string Method, string PaypalOrderId, bool DoSigurim = false);
public record CreateOrderDto(int CarId, DateOnly DataFillimit, DateOnly DataPerfundimit, string Method, string? ReturnUrl = null, string? CancelUrl = null, bool DoSigurim = false, string? BiletaKey = null);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IPayPalService _payPal;

    // Per-day platform markup built into the customer-facing price — the business sets their rate
    // and the customer sees that rate + PlatformMarkup. Never a separate line item.
    private const decimal PlatformMarkup = 5m;

    public PaymentsController(ERentalDbContext context, IPayPalService payPal)
    {
        _context = context;
        _payPal = payPal;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Demo accounts pay through PayPal Sandbox, so they may only book cars of demo businesses
    // (owner is a demo account) â€” and real clients may never pay real money to a demo business.
    internal static string? DemoMismatch(User user, Car car)
    {
        bool demoBusiness = car.Company?.OwnerUser?.IsDemo == true;
        if (user.IsDemo && !demoBusiness) return "Llogarite demo mund te rezervojne vetem makina te bizneseve demo.";
        if (!user.IsDemo && demoBusiness) return "Kjo makine eshte vetem per demo dhe nuk mund te rezervohet.";
        return null;
    }

    // How much of the client's credit goes toward an online payment of `shuma`. PayPal needs a
    // positive amount, so at least 1€ is always left to pay. Same rule in create-order, capture and
    // POST /Bookings, so all three agree on the amount actually charged.
    internal static decimal CreditApplied(User user, decimal shuma) =>
        user.Kredi <= 0 ? 0m : Math.Min(user.Kredi, Math.Max(0m, shuma - 1m));

    // Creates the PayPal order server-side (amount computed from the car/dates, never trusting the
    // client). When returnUrl/cancelUrl are given, PayPal's redirect-based checkout is used â€” the
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

        var car = await _context.Cars.Include(c => c.PriceOffers).Include(c => c.PricePeriods).Include(c => c.Company).ThenInclude(co => co.OwnerUser).Include(c => c.Company).ThenInclude(co => co.SeasonRules).FirstOrDefaultAsync(c => c.CarId == dto.CarId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        var demoMismatch = DemoMismatch(user, car);
        if (demoMismatch != null) return BadRequest(demoMismatch);

        if (dto.DataPerfundimit <= dto.DataFillimit)
            return BadRequest("Datat nuk jane te vlefshme.");

        int dite = dto.DataPerfundimit.DayNumber - dto.DataFillimit.DayNumber;
        int minDite = car.MinimumDitesh ?? car.Company?.MinimumDitesh ?? 1;
        if (dite < minDite)
            return BadRequest($"Minimumi i qerase per kete makine eshte {minDite} dite.");

        decimal sigurimi = 0;
        if (dto.DoSigurim)
        {
            if (car.Company?.CmimiSigurimit == null)
                return BadRequest("Ky biznes nuk ofron sigurim te plote.");
            sigurimi = car.Company.CmimiSigurimit.Value;
        }

        if (dto.Method == "deposit" && car.VetemPagesePlote)
            return BadRequest("Kjo makine pranon vetem pagese te plote.");
        if (car.Company?.KerkoBileten == true && !BookingsController.IsOwnTicketKey(dto.BiletaKey, user.UserId))
            return BadRequest("Ky biznes kerkon biletën e avionit. Ngarko biletën para se te paguash.");

        var quote = CarPricing.Compute(car, dto.DataFillimit, dto.DataPerfundimit);
        decimal rentalTotal = quote.RentalTotal;
        decimal customerTotal = rentalTotal + dite * PlatformMarkup;
        // Insurance only rides along with a full online payment -- with a deposit, it's collected
        // in cash at pickup alongside the rest of the rental, same as the un-deposited rental balance,
        // so a no-show only costs the client the (non-refundable) deposit, not the insurance too.
        decimal shuma = dto.Method == "deposit" ? quote.FirstDayPrice + PlatformMarkup : customerTotal + sigurimi;
        shuma -= CreditApplied(user, shuma);

        var result = await _payPal.ForDemo(user.IsDemo).CreateOrderAsync(shuma, "EUR", dto.ReturnUrl, dto.CancelUrl);
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

        var user = await _context.Users.FindAsync(GetUserId());
        if (user == null) return Unauthorized();

        var car = await _context.Cars.Include(c => c.PriceOffers).Include(c => c.PricePeriods).Include(c => c.Company).ThenInclude(co => co.OwnerUser).Include(c => c.Company).ThenInclude(co => co.SeasonRules).FirstOrDefaultAsync(c => c.CarId == dto.CarId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        var demoMismatch = DemoMismatch(user, car);
        if (demoMismatch != null) return BadRequest(demoMismatch);

        if (dto.DataPerfundimit <= dto.DataFillimit)
            return BadRequest("Datat nuk jane te vlefshme.");

        int dite = dto.DataPerfundimit.DayNumber - dto.DataFillimit.DayNumber;
        int minDite = car.MinimumDitesh ?? car.Company?.MinimumDitesh ?? 1;
        if (dite < minDite)
            return BadRequest($"Minimumi i qerase per kete makine eshte {minDite} dite.");

        decimal sigurimi = 0;
        if (dto.DoSigurim)
        {
            if (car.Company?.CmimiSigurimit == null)
                return BadRequest("Ky biznes nuk ofron sigurim te plote.");
            sigurimi = car.Company.CmimiSigurimit.Value;
        }

        if (dto.Method == "deposit" && car.VetemPagesePlote)
            return BadRequest("Kjo makine pranon vetem pagese te plote.");

        var quote = CarPricing.Compute(car, dto.DataFillimit, dto.DataPerfundimit);
        decimal rentalTotal = quote.RentalTotal;
        decimal customerTotal = rentalTotal + dite * PlatformMarkup;
        decimal pritshme = dto.Method == "deposit" ? quote.FirstDayPrice + PlatformMarkup : customerTotal + sigurimi;
        pritshme -= CreditApplied(user, pritshme);

        var payPal = _payPal.ForDemo(user.IsDemo);
        var result = await payPal.CaptureOrderAsync(dto.PaypalOrderId);
        if (!result.Success)
            return BadRequest(result.Error ?? "Pagesa nuk u pranua nga PayPal.");

        if (result.Amount == null || Math.Abs(result.Amount.Value - pritshme) > 0.01m)
        {
            if (result.CaptureId != null)
                await payPal.RefundCaptureAsync(result.CaptureId, result.Amount ?? pritshme, result.Currency ?? "EUR");
            return BadRequest("Shuma e paguar nuk perputhet me cmimin e pritshem. Pagesa u rimbursua automatikisht.");
        }

        return Ok(new { captureId = result.CaptureId, amountPaid = result.Amount, cardLast4 = result.CardLast4 });
    }

    // Transaction ledger for the caller's own business â€” reference number, amounts, and who/what it was for.
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
                Booking = new { p.Booking.BookingId, p.Booking.DataFillimit, p.Booking.DataPerfundimit, p.Booking.IsDemo },
                Car = new { p.Booking.Car.Marka, p.Booking.Car.Modeli },
                Klienti = new { p.Booking.User.Emri, p.Booking.User.Mbiemri }
            })
            .ToListAsync();

        return Ok(payments);
    }

    // Platform-wide transaction ledger, every business â€” admin only.
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
                Booking = new { p.Booking.BookingId, p.Booking.DataFillimit, p.Booking.DataPerfundimit, p.Booking.IsDemo },
                Car = new { p.Booking.Car.Marka, p.Booking.Car.Modeli },
                Klienti = new { p.Booking.User.Emri, p.Booking.User.Mbiemri },
                Biznesi = new { p.Booking.Car.Company.CompanyId, p.Booking.Car.Company.Emri }
            })
            .ToListAsync();

        return Ok(payments);
    }
}
