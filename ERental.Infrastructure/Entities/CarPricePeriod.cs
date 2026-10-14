namespace ERental.Infrastructure.Entities;

// A fixed daily price for one car over an inclusive date range (e.g. 1-15 Jan: 70/day).
// Overrides the car's normal CmimiDites and any company season rule for those days.
public partial class CarPricePeriod
{
    public int Id { get; set; }

    public int CarId { get; set; }

    public DateOnly DataFillimit { get; set; }

    public DateOnly DataPerfundimit { get; set; }

    public decimal CmimiDites { get; set; }

    public virtual Car Car { get; set; } = null!;
}
