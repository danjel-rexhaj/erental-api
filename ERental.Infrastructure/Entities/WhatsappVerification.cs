using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class WhatsappVerification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Code { get; set; } = null!;

    public string? Statusi { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public DateTime? DataShqyrtimit { get; set; }

    public virtual User User { get; set; } = null!;
}
