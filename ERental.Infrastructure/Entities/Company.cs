using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

    public bool? AllowCashPayment { get; set; }

    public double? AvgRating { get; set; }

    public int ReviewCount { get; set; }

    public int CarCount { get; set; }

    // Bank details for commission payouts — never serialized by default (public endpoints like
    // GetCars/GetCompanies return this entity as-is); only exposed via explicit owner/admin
    // projections in CompaniesController.
    [JsonIgnore]
    public string? Iban { get; set; }

    public virtual ICollection<Car> Cars { get; set; } = new List<Car>();

    public virtual ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();

    public virtual ICollection<CompanyVerification> CompanyVerifications { get; set; } = new List<CompanyVerification>();

    public virtual User? OwnerUser { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
