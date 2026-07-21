using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int BookingId { get; set; }

    public decimal ShumaTotale { get; set; }

    public decimal? Komisioni { get; set; }

    public decimal ShumaBiznesit { get; set; }

    public DateTime? DataPageses { get; set; }

    public string? MetodaPageses { get; set; }

    public string? Statusi { get; set; }

    public string? PaypalOrderId { get; set; }

    public string? PaypalCaptureId { get; set; }

    public decimal? ShumaPaguarOnline { get; set; }

    public string? CardLast4 { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
