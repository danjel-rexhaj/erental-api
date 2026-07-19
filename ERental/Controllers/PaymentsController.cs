using ERental.Application.Interfaces;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    // Creates the PayPal order server-side (amount computed from the car/dates, never trusting the
    // client). When returnUrl/cancelUrl are given, PayPal's redirect-based checkout is used — the
    // frontend sends the browser to the returned approveUrl instead of rendering an inline lightbox.
    [HttpPost("paypal/create-order")]
    [Authorize]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        if (dto.Method != "deposit" && dto.Method != "full")
            return BadRequest("Menyre pagese e panjohur.");

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
}
