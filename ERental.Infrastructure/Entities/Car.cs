using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class Car
{
    public int CarId { get; set; }

    public int CompanyId { get; set; }

    public string Marka { get; set; } = null!;

    public string Modeli { get; set; } = null!;

    public int Viti { get; set; }

    public int? Km { get; set; }

    public string? Ngjyra { get; set; }

    public string? Targa { get; set; }

    public int? NumriVendeve { get; set; }

    public bool? Klimatizimi { get; set; }

    public decimal CmimiDites { get; set; }

    public string? Karburanti { get; set; }

    public string? Transmisioni { get; set; }

    public string? Kategoria { get; set; }

    public string? Statusi { get; set; }

    public string? Pershkrimi { get; set; }

    public int? Kubatura { get; set; }

    public int? Cilindra { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public string[]? Amenities { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<CarAvailabilityBlock> CarAvailabilityBlocks { get; set; } = new List<CarAvailabilityBlock>();

    public virtual ICollection<CarPhoto> CarPhotos { get; set; } = new List<CarPhoto>();

    public virtual ICollection<CarView> CarViews { get; set; } = new List<CarView>();

    public virtual Company Company { get; set; } = null!;
}
