using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class Company
{
    public int CompanyId { get; set; }

    public string Emri { get; set; } = null!;

    public string? Email { get; set; }

    public string? Telefoni { get; set; }

    public string? Adresa { get; set; }

    public string? Qyteti { get; set; }

    public string Nipt { get; set; } = null!;

    public bool? EshteVerifikuar { get; set; }

    public DateTime? DataVerifikimit { get; set; }

    public decimal? CommissionRate { get; set; }

    public DateTime? DataRegjistrimit { get; set; }

    public string? BillingModel { get; set; }

    public string? Statusi { get; set; }

    public int? OwnerUserId { get; set; }

    public string? LogoUrl { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public virtual ICollection<Car> Cars { get; set; } = new List<Car>();

    public virtual ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();

    public virtual ICollection<CompanyVerification> CompanyVerifications { get; set; } = new List<CompanyVerification>();

    public virtual User? OwnerUser { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
