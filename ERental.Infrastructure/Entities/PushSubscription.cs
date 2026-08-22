using System;

namespace ERental.Infrastructure.Entities;

public partial class PushSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Endpoint { get; set; } = null!;

    public string P256dh { get; set; } = null!;

    public string Auth { get; set; } = null!;

    public string? UserAgent { get; set; }

    public DateTime? DataKrijimit { get; set; }

    public virtual User User { get; set; } = null!;
}
