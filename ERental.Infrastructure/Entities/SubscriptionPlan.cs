using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class SubscriptionPlan
{
    public int PlanId { get; set; }

    public string Emri { get; set; } = null!;

    public decimal CmimiMujor { get; set; }

    public int? LimitMakinash { get; set; }

    public string? Pershkrimi { get; set; }

    public virtual ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();
}
