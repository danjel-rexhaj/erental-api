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

    public decimal CmimiTotal { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public string? Statusi { get; set; }

    public string? ArsyejaRefuzimit { get; set; }

    public virtual Car Car { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual User User { get; set; } = null!;
}
