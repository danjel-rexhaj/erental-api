using System;

namespace ERental.Infrastructure.Entities;

public partial class Favorite
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int CarId { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Car Car { get; set; } = null!;
}
