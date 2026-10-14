namespace ERental.Infrastructure.Entities;

// Company-wide season: every car of the company costs ShtesaDites more per day for days in the
// inclusive date range (unless that car has its own CarPricePeriod covering the day).
public partial class CompanySeasonRule
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public DateOnly DataFillimit { get; set; }

    public DateOnly DataPerfundimit { get; set; }

    public decimal ShtesaDites { get; set; }

    public virtual Company Company { get; set; } = null!;
}
