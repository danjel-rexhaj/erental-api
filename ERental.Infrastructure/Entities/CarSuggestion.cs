using System;

namespace ERental.Infrastructure.Entities;

public partial class CarSuggestion
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    // "brand" when the business typed a whole new make via "Tjeter"; "model" when the make was
    // known but the model was typed via "Tjeter" (Marka then holds that known make, for context).
    public string Type { get; set; } = null!;

    public string? Marka { get; set; }

    public string SuggestedValue { get; set; } = null!;

    public string Statusi { get; set; } = "pending";

    public DateTime? DataKrijimit { get; set; }

    public virtual Company Company { get; set; } = null!;
}
