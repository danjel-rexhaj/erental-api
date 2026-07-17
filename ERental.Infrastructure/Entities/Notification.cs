using System;
using System.Collections.Generic;
using System.Text;


namespace ERental.Infrastructure.Entities;

public partial class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool? IsRead { get; set; }
    public DateTime? DataKrijimit { get; set; }
    public int? BookingId { get; set; }
    public string? Target { get; set; }
}