using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CarPhoto
{
    public int PhotoId { get; set; }

    public int CarId { get; set; }

    public string UrlFotos { get; set; } = null!;

    public bool? EshteKryesore { get; set; }

    public DateTime? DataNgarkimit { get; set; }

    public string? Kategoria { get; set; }

    public virtual Car Car { get; set; } = null!;
}
