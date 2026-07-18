using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CarView
{
    public int Id { get; set; }

    public int CarId { get; set; }

    public int? UserId { get; set; }

    public string? IpAddress { get; set; }

    public DateTime? DataShikimit { get; set; }

    public virtual Car Car { get; set; } = null!;
}
