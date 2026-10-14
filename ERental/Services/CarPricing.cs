using ERental.Infrastructure.Entities;

namespace ERental.Services;

// Single source of truth for what a rental costs, used by booking creation and both PayPal steps
// (they must agree to the cent or the capture is rejected). src/carData.js computeRentalQuote()
// mirrors this for display -- keep the two in sync.
//
// Each rental day (start inclusive, end exclusive) is priced on its own:
//   1. a CarPricePeriod covering the day -> that fixed price;
//   2. else car.CmimiDites + the largest CompanySeasonRule.ShtesaDites covering the day.
// If no day hit a period or season, the multi-day offer for that exact length (if any) applies
// as before; seasons take priority over offers.
// Callers must load car.PricePeriods, car.PriceOffers and car.Company.SeasonRules.
public static class CarPricing
{
    public record Quote(decimal RentalTotal, decimal FirstDayPrice, bool Seasonal);

    public static Quote Compute(Car car, DateOnly from, DateOnly to)
    {
        int days = to.DayNumber - from.DayNumber;
        decimal sum = 0, first = car.CmimiDites;
        bool seasonal = false;

        for (int i = 0; i < days; i++)
        {
            var price = DayPrice(car, from.AddDays(i), out var special);
            seasonal |= special;
            sum += price;
            if (i == 0) first = price;
        }

        if (!seasonal)
        {
            var offer = car.PriceOffers.FirstOrDefault(o => o.Dite == days);
            sum = offer?.CmimiTotal ?? days * car.CmimiDites;
        }

        return new Quote(sum, first, seasonal);
    }

    private static decimal DayPrice(Car car, DateOnly day, out bool special)
    {
        var period = car.PricePeriods.FirstOrDefault(p => p.DataFillimit <= day && day <= p.DataPerfundimit);
        if (period != null) { special = true; return period.CmimiDites; }

        var seasons = car.Company?.SeasonRules.Where(s => s.DataFillimit <= day && day <= s.DataPerfundimit).ToList();
        if (seasons != null && seasons.Count > 0) { special = true; return car.CmimiDites + seasons.Max(s => s.ShtesaDites); }

        special = false;
        return car.CmimiDites;
    }
}
