using ERental.Application.Interfaces;
using ERental.Hubs;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ERental.Services;

public class BookingCompletionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingCompletionService> _logger;

    public BookingCompletionService(IServiceScopeFactory scopeFactory, ILogger<BookingCompletionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CompleteExpiredBookingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gabim gjate perfundimit automatik te rezervimeve.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task CompleteExpiredBookingsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ERentalDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expired = await context.Bookings
            .Include(b => b.Car).ThenInclude(c => c.Company)
            .Include(b => b.User)
            .Where(b => b.Statusi == "confirmed" && b.DataPerfundimit < today)
            .ToListAsync(ct);

        foreach (var booking in expired)
        {
            booking.Statusi = "completed";

            var notif = new Notification
            {
                UserId = booking.UserId,
                Title = "Si ishte qeraja?",
                Message = $"Le nje vleresim per {booking.Car.Marka} {booking.Car.Modeli} dhe {booking.Car.Company.Emri}",
                IsRead = false,
                BookingId = booking.BookingId,
                Target = "leave_review"
            };
            context.Notifications.Add(notif);
            await context.SaveChangesAsync(ct);

            try
            {
                await hub.Clients.Group(booking.UserId.ToString()).SendAsync("notification", new
                {
                    id = notif.Id,
                    title = notif.Title,
                    message = notif.Message,
                    createdAt = notif.DataKrijimit,
                    bookingId = notif.BookingId,
                    target = notif.Target
                }, ct);
            }
            catch { }

            try
            {
                await emailService.SendReviewRequestAsync(booking.User.Email, booking.User.Emri, $"{booking.Car.Marka} {booking.Car.Modeli}", booking.Car.Company.Emri);
            }
            catch { }
        }
    }
}
