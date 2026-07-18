using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class LoginLog
{
    public int Id { get; set; }

    public string? Email { get; set; }

    public int? UserId { get; set; }

    public string? IpAddress { get; set; }

    public bool Sukses { get; set; }

    public DateTime? DataHyrjes { get; set; }
}
