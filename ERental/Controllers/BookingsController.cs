using ERental.Application.Interfaces;
using ERental.Hubs;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERental.Controllers;

public record CreateBookingDto(int CarId, DateOnly DataFillimit, DateOnly DataPerfundimit, string? PaymentMethod = null, string? PaypalCaptureId = null);
public record CancelBookingDto(string? Reason);

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IPayPalService _payPal;

    public BookingsController(ERentalDbContext context, IEmailService emailService, IHubContext<NotificationHub> hub, IPayPalService payPal)
    {
        _context = context;
        _emailService = emailService;
        _hub = hub;
        _payPal = payPal;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static readonly string[] MuajtSq = { "Janar", "Shkurt", "Mars", "Prill", "Maj", "Qershor", "Korrik", "Gusht", "Shtator", "Tetor", "Nentor", "Dhjetor" };
    private static string FormatDateSq(DateOnly d) => $"{d.Day} {MuajtSq[d.Month - 1]} {d.Year}";

    private async Task NotifyAsync(int userId, string title, string message, int? bookingId = null, string? target = null)
    {
        var notif = new Notification { UserId = userId, Title = title, Message = message, IsRead = false, BookingId = bookingId, Target = target };
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync();

        await _hub.Clients.Group(userId.ToString()).SendAsync("notification", new
        {
            id = notif.Id,
            title = notif.Title,
            message = notif.Message,
            createdAt = notif.DataKrijimit,
            bookingId = notif.BookingId,
            target = notif.Target
        });
    }

    // Cancelled bookings are kept visible for 24h (so both sides can still see why/what happened),
    // then swept on the next list load — no background job needed for this volume.
    private async Task PurgeExpiredCancelledAsync()
    {
        var cutoff = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-24), DateTimeKind.Unspecified);
        var expired = await _context.Bookings
            .Where(b => b.Statusi == "cancelled" && b.DataAnulimit != null && b.DataAnulimit < cutoff)
            .ToListAsync();
        if (expired.Count == 0) return;

        var ids = expired.Select(b => b.BookingId).ToList();
        _context.Payments.RemoveRange(_context.Payments.Where(p => ids.Contains(p.BookingId)));
        _context.Reviews.RemoveRange(_context.Reviews.Where(r => ids.Contains(r.BookingId)));
        _context.Bookings.RemoveRange(expired);
        await _context.SaveChangesAsync();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateBooking(CreateBookingDto dto)
    {
        var userId = GetUserId();

        var car = await _context.Cars.Include(c => c.Company).Include(c => c.CarPhotos).FirstOrDefaultAsync(c => c.CarId == dto.CarId);
        if (car == null) return NotFound("Makina nuk ekziston.");

        if (dto.DataPerfundimit <= dto.DataFillimit)
            return BadRequest("Data e perfundimit duhet te jete pas dates se fillimit.");

        // Boundaries touching (previous rental ends the day this one would start) are not a conflict.
        var konfliktet = await _context.CarAvailabilityBlocks
            .Where(b => b.CarId == dto.CarId
                && b.DataFillimit < dto.DataPerfundimit
                && b.DataPerfundimit > dto.DataFillimit)
            .Select(b => b.DataPerfundimit)
            .ToListAsync();

        var paymentMethod = dto.PaymentMethod;
        if (paymentMethod != "paypal_deposit" && paymentMethod != "paypal_full")
            return BadRequest("Duhet zgjedhur nje menyre pagese (depozite ose e plote).");

        int diteRezervimi = dto.DataPerfundimit.DayNumber - dto.DataFillimit.DayNumber;
        decimal cmimiTotal = diteRezervimi * car.CmimiDites;

        // The capture already happened via POST /api/Payments/paypal/capture — re-verify it here
        // against PayPal directly rather than trusting the client's claimed amount/method.
        decimal? shumaPaguarOnline;
        {
            if (string.IsNullOrWhiteSpace(dto.PaypalCaptureId))
                return BadRequest("Mungon konfirmimi i pageses.");

            var capture = await _payPal.GetCaptureAsync(dto.PaypalCaptureId);
            if (!capture.Success)
                return BadRequest("Pagesa nuk u konfirmua nga PayPal.");

            var pritshme = paymentMethod == "paypal_deposit" ? car.CmimiDites : cmimiTotal;
            if (capture.Amount == null || Math.Abs(capture.Amount.Value - pritshme) > 0.01m)
                return BadRequest("Shuma e paguar nuk perputhet me rezervimin.");

            shumaPaguarOnline = capture.Amount;
        }

        if (konfliktet.Count > 0)
        {
            var lirohetMe = konfliktet.Max();
            await _payPal.RefundCaptureAsync(dto.PaypalCaptureId!, shumaPaguarOnline!.Value);
            return BadRequest($"Makina eshte e zene per keto data. Lirohet me {FormatDateSq(lirohetMe)}.");
        }

        var booking = new Booking
        {
            UserId = userId,
            CarId = dto.CarId,
            DataFillimit = dto.DataFillimit,
            DataPerfundimit = dto.DataPerfundimit,
            CmimiTotal = cmimiTotal,
            Statusi = "pending",
            PaymentMethod = paymentMethod
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        {
            var company = car.Company;
            decimal komisioni = company.BillingModel == "commission" ? cmimiTotal * (company.CommissionRate ?? 0) / 100 : 0;
            _context.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                ShumaTotale = cmimiTotal,
                Komisioni = komisioni,
                ShumaBiznesit = cmimiTotal - komisioni,
                ShumaPaguarOnline = shumaPaguarOnline,
                MetodaPageses = paymentMethod,
                PaypalCaptureId = dto.PaypalCaptureId,
                Statusi = "completed"
            });
            await _context.SaveChangesAsync();
        }

        var klienti = await _context.Users.FindAsync(userId);
        var makinaEmri = $"{car.Marka} {car.Modeli}";
        var carPhotoUrl = car.CarPhotos.FirstOrDefault(p => p.EshteKryesore == true)?.UrlFotos ?? car.CarPhotos.FirstOrDefault()?.UrlFotos;

        try
        {
            if (klienti != null)
                await _emailService.SendBookingPendingToClientAsync(klienti.Email, klienti.Emri, makinaEmri, dto.DataFillimit.ToString(), dto.DataPerfundimit.ToString(), cmimiTotal, booking.BookingId, carPhotoUrl);

            if (car.Company.Email != null)
                await _emailService.SendBookingRequestToBusinessAsync(car.Company.Email, car.Company.Emri, makinaEmri, klienti?.Emri ?? "Klient", dto.DataFillimit.ToString(), dto.DataPerfundimit.ToString(), carPhotoUrl);

            if (shumaPaguarOnline != null)
            {
                bool eshtePagesePlote = paymentMethod == "paypal_full";
                if (klienti != null)
                    await _emailService.SendPaymentReceiptAsync(klienti.Email, klienti.Emri, makinaEmri, car.Company.Emri, shumaPaguarOnline.Value, eshtePagesePlote, booking.BookingId, perBiznesin: false);
                if (car.Company.Email != null)
                    await _emailService.SendPaymentReceiptAsync(car.Company.Email, car.Company.Emri, makinaEmri, klienti?.Emri ?? "Klient", shumaPaguarOnline.Value, eshtePagesePlote, booking.BookingId, perBiznesin: true);
            }
        }
        catch (Exception ex) { Console.WriteLine($"CreateBooking email error: {ex.Message}"); }

        try
        {
            if (car.Company.OwnerUserId != null)
                await NotifyAsync(car.Company.OwnerUserId.Value, "Kerkese e re rezervimi", $"{klienti?.Emri ?? "Nje klient"} kerkoi te rezervoje {makinaEmri}", booking.BookingId, "business_booking");
        }
        catch { }

        return Ok(booking);
    }

    [HttpPut("{id}/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmBooking(int id)
    {
        var userId = GetUserId();

        var booking = await _context.Bookings
            .Include(b => b.Car)
                .ThenInclude(c => c.Company)
            .Include(b => b.Car)
                .ThenInclude(c => c.CarPhotos)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
            return NotFound("Rezervimi nuk u gjet.");


        if (booking.Car.Company.OwnerUserId != userId)
            return Forbid();


        if (booking.Statusi == "confirmed")
            return BadRequest("Ky rezervim eshte konfirmuar tashme.");


        if (booking.Statusi != "pending")
            return BadRequest("Ky rezervim nuk mund te konfirmohet.");


        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Ndrysho statusin
            booking.Statusi = "confirmed";


            // Kontrollo nese ekziston bllokimi i makines
            var blockExists = await _context.CarAvailabilityBlocks
                .AnyAsync(b => b.Shenim == $"Booking #{booking.BookingId}");


            if (!blockExists)
            {
                var block = new CarAvailabilityBlock
                {
                    CarId = booking.CarId,
                    DataFillimit = booking.DataFillimit,
                    DataPerfundimit = booking.DataPerfundimit,
                    Arsyeja = "booked",
                    Shenim = $"Booking #{booking.BookingId}"
                };

                _context.CarAvailabilityBlocks.Add(block);
            }


            // Llogarit pagesen
            var company = booking.Car.Company;

            decimal komisioni = 0;

            if (company.BillingModel == "commission")
            {
                komisioni = booking.CmimiTotal *
                            (company.CommissionRate ?? 0) / 100;
            }


            decimal shumaBiznesit = booking.CmimiTotal - komisioni;


            // Kontrollo nese ekziston payment
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId);


            if (payment == null)
            {
                payment = new Payment
                {
                    BookingId = booking.BookingId,
                    ShumaTotale = booking.CmimiTotal,
                    Komisioni = komisioni,
                    ShumaBiznesit = shumaBiznesit,
                    MetodaPageses = booking.PaymentMethod ?? "cash",
                    Statusi = "completed"
                };

                _context.Payments.Add(payment);
            }


            await _context.SaveChangesAsync();

            await transaction.CommitAsync();


            // Email klientit
            try
            {
                var carPhotoUrl = booking.Car.CarPhotos.FirstOrDefault(p => p.EshteKryesore == true)?.UrlFotos ?? booking.Car.CarPhotos.FirstOrDefault()?.UrlFotos;
                await _emailService.SendBookingConfirmedAsync(
                    booking.User.Email,
                    booking.User.Emri,
                    $"{booking.Car.Marka} {booking.Car.Modeli}",
                    company.Emri,
                    booking.DataFillimit.ToString(),
                    booking.DataPerfundimit.ToString(),
                    booking.CmimiTotal,
                    booking.BookingId,
                    company.Adresa,
                    company.Qyteti,
                    company.Telefoni,
                    carPhotoUrl
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfirmBooking email error: {ex.Message}");
            }


            // Notification
            try
            {
                await NotifyAsync(
                    booking.UserId,
                    "Rezervimi u konfirmua",
                    $"{company.Emri} e konfirmoi rezervimin per {booking.Car.Marka} {booking.Car.Modeli}",
                    booking.BookingId,
                    "client_booking"
                );
            }
            catch
            {
            }


            return Ok(new
            {
                message = "Rezervimi u konfirmua me sukses.",
                booking,
                payment
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return BadRequest(new
            {
                message = "Ndodhi nje gabim gjate konfirmimit.",
                error = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpPut("{id}/verify-id")]
    [Authorize]
    public async Task<IActionResult> VerifyId(int id)
    {
        var userId = GetUserId();

        var booking = await _context.Bookings
            .Include(b => b.Car).ThenInclude(c => c.Company)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();

        if (booking.Car.Company.OwnerUserId != userId) return Forbid();
        if (booking.Statusi != "pending" && booking.Statusi != "confirmed")
            return BadRequest("Ky rezervim nuk mund te verifikohet.");

        booking.IdVerifikuar = true;
        await _context.SaveChangesAsync();

        try
        {
            await NotifyAsync(booking.UserId, "Identiteti u verifikua",
                $"{booking.Car.Company.Emri} konfirmoi identitetin tend — je gati per te marre makinen.",
                booking.BookingId, "client_booking");
        }
        catch { }

        return Ok(new { message = "Identiteti u verifikua.", booking });
    }

    [HttpPut("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelBooking(int id, CancelBookingDto? dto = null)
    {
        var userId = GetUserId();

        var booking = await _context.Bookings
            .Include(b => b.Car).ThenInclude(c => c.Company)
            .Include(b => b.Car).ThenInclude(c => c.CarPhotos)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();

        bool eshteKlienti = booking.UserId == userId;
        bool eshteBiznesi = booking.Car.Company.OwnerUserId == userId;
        if (!eshteKlienti && !eshteBiznesi) return Forbid();

        if (booking.Statusi == "cancelled") return BadRequest("Ky rezervim eshte anuluar tashme.");
        if (booking.Statusi == "completed") return BadRequest("S'mund te anulosh nje rezervim te perfunduar.");

        if (eshteKlienti && !eshteBiznesi)
        {
            var oreQeKaluan = (DateTime.UtcNow - booking.DataKrijimit!.Value).TotalHours;
            if (oreQeKaluan > 12)
                return BadRequest("Kane kaluar 12 ore nga rezervimi — anulimi nuk lejohet me nga platforma. Kontakto biznesin direkt.");
        }

        if (eshteBiznesi && string.IsNullOrWhiteSpace(dto?.Reason))
            return BadRequest("Duhet te jepesh nje arsye per refuzimin.");

        booking.Statusi = "cancelled";
        booking.DataAnulimit = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        if (eshteBiznesi) booking.ArsyejaRefuzimit = dto!.Reason;

        var block = await _context.CarAvailabilityBlocks
            .FirstOrDefaultAsync(b => b.CarId == booking.CarId && b.Shenim == $"Booking #{booking.BookingId}");
        if (block != null) _context.CarAvailabilityBlocks.Remove(block);

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.BookingId);
        if (payment != null)
        {
            if (!string.IsNullOrEmpty(payment.PaypalCaptureId) && payment.ShumaPaguarOnline is > 0)
            {
                try { await _payPal.RefundCaptureAsync(payment.PaypalCaptureId, payment.ShumaPaguarOnline.Value); }
                catch { }
            }
            payment.Statusi = "refunded";
        }

        await _context.SaveChangesAsync();

        try
        {
            var carPhotoUrl = booking.Car.CarPhotos.FirstOrDefault(p => p.EshteKryesore == true)?.UrlFotos ?? booking.Car.CarPhotos.FirstOrDefault()?.UrlFotos;
            if (eshteBiznesi)
            {
                await _emailService.SendBookingCancelledAsync(
                    booking.User.Email, booking.User.Emri,
                    $"{booking.Car.Marka} {booking.Car.Modeli}",
                    booking.DataFillimit.ToString(), booking.DataPerfundimit.ToString(), booking.BookingId, carPhotoUrl, booking.ArsyejaRefuzimit);
            }
            else
            {
                if (booking.Car.Company.Email != null)
                    await _emailService.SendBookingCancelledAsync(
                        booking.Car.Company.Email, booking.Car.Company.Emri,
                        $"{booking.Car.Marka} {booking.Car.Modeli}",
                        booking.DataFillimit.ToString(), booking.DataPerfundimit.ToString(), booking.BookingId, carPhotoUrl);
            }
        }
        catch (Exception ex) { Console.WriteLine($"CancelBooking email error: {ex.Message}"); }

        try
        {
            var targetUserId = eshteBiznesi ? booking.UserId : booking.Car.Company.OwnerUserId;
            var notifTarget = eshteBiznesi ? "client_booking" : "business_booking";
            var notifMsg = eshteBiznesi && !string.IsNullOrWhiteSpace(booking.ArsyejaRefuzimit)
                ? $"Rezervimi per {booking.Car.Marka} {booking.Car.Modeli} u refuzua. Arsyeja: {booking.ArsyejaRefuzimit}"
                : $"Rezervimi per {booking.Car.Marka} {booking.Car.Modeli} u anulua";
            if (targetUserId != null)
                await NotifyAsync(targetUserId.Value, "Rezervimi u anulua", notifMsg, booking.BookingId, notifTarget);
        }
        catch { }

        return Ok(new { message = "Rezervimi u anulua.", booking });
    }

    [HttpGet("for-my-company")]
    [Authorize]
    public async Task<IActionResult> GetCompanyBookings()
    {
        var userId = GetUserId();
        await PurgeExpiredCancelledAsync();

        var bookings = await _context.Bookings
            .Include(b => b.Car).ThenInclude(c => c.Company)
            .Include(b => b.User)
            .Include(b => b.Payments)
            .Where(b => b.Car.Company.OwnerUserId == userId)
            .OrderByDescending(b => b.DataKrijimit)
            .Select(b => new
            {
                b.BookingId,
                b.DataFillimit,
                b.DataPerfundimit,
                b.CmimiTotal,
                b.Statusi,
                b.DataKrijimit,
                b.ArsyejaRefuzimit,
                b.PaymentMethod,
                b.IdVerifikuar,
                Car = new { b.Car.Marka, b.Car.Modeli, b.Car.Targa },
                Klienti = new { b.User.Emri, b.User.Mbiemri, b.User.Telefoni, b.User.Email, b.User.HasWhatsapp },
                Payment = b.Payments.Select(p => new { p.ShumaPaguarOnline, p.PaypalCaptureId }).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = GetUserId();
        await PurgeExpiredCancelledAsync();
        var bookings = await _context.Bookings
            .Include(b => b.Car).ThenInclude(c => c.Company)
            .Include(b => b.Reviews)
            .Include(b => b.Payments)
            .Where(b => b.UserId == userId)
            .ToListAsync();
        return Ok(bookings);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        var userId = GetUserId();

        var booking = await _context.Bookings
            .Include(b => b.Car).ThenInclude(c => c.Company)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();

        if (booking.Car.Company.OwnerUserId != userId && booking.UserId != userId) return Forbid();
        if (booking.Statusi != "cancelled") return BadRequest("Vetem rezervimet e anuluara/refuzuara mund te fshihen.");

        _context.Payments.RemoveRange(_context.Payments.Where(p => p.BookingId == id));
        _context.Reviews.RemoveRange(_context.Reviews.Where(r => r.BookingId == id));
        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Rezervimi u fshi." });
    }
}