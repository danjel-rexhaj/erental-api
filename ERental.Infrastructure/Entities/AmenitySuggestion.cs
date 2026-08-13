using System;

namespace ERental.Infrastructure.Entities;

public partial class AmenitySuggestion
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public string Suggestion { get; set; } = null!;

    public string Statusi { get; set; } = "pending";

    public DateTime? DataKrijimit { get; set; }

    public virtual Company Company { get; set; } = null!;
}
