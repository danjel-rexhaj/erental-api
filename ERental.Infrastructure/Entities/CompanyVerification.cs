using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CompanyVerification
{
    public int VerificationId { get; set; }

    public int CompanyId { get; set; }

    public string Nipt { get; set; } = null!;

    public string CertifikataUrl { get; set; } = null!;

    public DateTime? DataDorezimit { get; set; }

    public DateTime? DataShqyrtimit { get; set; }

    public string? ShenimeAdmin { get; set; }

    public string? Statusi { get; set; }

    public virtual Company Company { get; set; } = null!;
}
