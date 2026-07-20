using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class LicenseView
{
    public int Id { get; set; }

    public int BookingId { get; set; }

    public int ViewedByUserId { get; set; }

    public DateTime? DataShikimit { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
