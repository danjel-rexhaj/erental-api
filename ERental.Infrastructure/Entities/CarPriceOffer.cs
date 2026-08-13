using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CarPriceOffer
{
    public int Id { get; set; }

    public int CarId { get; set; }

    public int Dite { get; set; }

    public decimal CmimiTotal { get; set; }

    public virtual Car Car { get; set; } = null!;
}
