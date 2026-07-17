using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class EmailVerification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime? DataKrijimit { get; set; }

    public DateTime DataSkadimit { get; set; }

    public bool? Perdorur { get; set; }

    public virtual User User { get; set; } = null!;
}
