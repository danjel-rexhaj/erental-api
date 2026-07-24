using System;
using System.Collections.Generic;
using System.Text;
using System;

namespace ERental.Infrastructure.Entities;

public partial class PendingRegistration
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Emri { get; set; } = null!;
    public string Mbiemri { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Telefoni { get; set; }
    public bool? HasWhatsapp { get; set; }
    public string? Kombesia { get; set; }
    public string Code { get; set; } = null!;
    public DateTime? DataKrijimit { get; set; }
    public DateTime DataSkadimit { get; set; }
}