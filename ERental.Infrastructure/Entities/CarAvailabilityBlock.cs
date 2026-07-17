using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CarAvailabilityBlock
{
    public int BlockId { get; set; }

    public int CarId { get; set; }

    public DateOnly DataFillimit { get; set; }

    public DateOnly DataPerfundimit { get; set; }

    public string? Shenim { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public string? Arsyeja { get; set; }

    public virtual Car Car { get; set; } = null!;
}
