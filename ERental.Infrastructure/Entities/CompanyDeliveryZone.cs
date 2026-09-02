using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CompanyDeliveryZone
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public string Zona { get; set; } = null!;

    public decimal Cmimi { get; set; }

    public virtual Company Company { get; set; } = null!;
}
