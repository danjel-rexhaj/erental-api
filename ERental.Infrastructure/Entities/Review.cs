using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class Review
{
    public int ReviewId { get; set; }

    public int UserId { get; set; }

    public int CompanyId { get; set; }

    public int BookingId { get; set; }

    public int? Rating { get; set; }

    public string? Koment { get; set; }

    public DateTime? Data { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Company Company { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
