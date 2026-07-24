using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Emri { get; set; } = null!;

    public string Mbiemri { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Telefoni { get; set; }

    public bool? EmailVerified { get; set; }

    public DateTime? DataRegjistrimit { get; set; }

    public bool? HasWhatsapp { get; set; }

    public bool? WhatsappVerified { get; set; }

    public string? FotoProfili { get; set; }

    public string? PatentaFotoPara { get; set; }

    public string? PatentaFotoMbrapa { get; set; }

    public string? Kombesia { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Company> Companies { get; set; } = new List<Company>();

    public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<WhatsappVerification> WhatsappVerifications { get; set; } = new List<WhatsappVerification>();
}
