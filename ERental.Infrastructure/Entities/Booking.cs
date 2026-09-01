using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class Booking
{
    public int BookingId { get; set; }

    public int UserId { get; set; }

    public int CarId { get; set; }

    public DateOnly DataFillimit { get; set; }

    public DateOnly DataPerfundimit { get; set; }

    // Client-picked pickup/return time (e.g. "10:00") -- purely informational for the business, never
    // checked against availability (that stays day-granular via CarAvailabilityBlocks).
    public string? OraMarrjes { get; set; }

    public string? OraKthimit { get; set; }

    public decimal CmimiTotal { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public string? Statusi { get; set; }

    public string? ArsyejaRefuzimit { get; set; }

    public string? PaymentMethod { get; set; }

    public bool IdVerifikuar { get; set; }

    public DateTime? DataAnulimit { get; set; }

    public Guid? ContractToken { get; set; }

    public DateTime? DataKonfirmimit { get; set; }

    // Snapshot of Company.CmimiSigurimit at booking time (not a live reference), so a later
    // change to the business's insurance price never retroactively alters past bookings.
    public decimal? CmimiSigurimit { get; set; }

    public virtual Car Car { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual User User { get; set; } = null!;
}
