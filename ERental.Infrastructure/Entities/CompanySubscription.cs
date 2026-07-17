using System;
using System.Collections.Generic;

namespace ERental.Infrastructure.Entities;

public partial class CompanySubscription
{
    public int SubscriptionId { get; set; }

    public int CompanyId { get; set; }

    public int PlanId { get; set; }

    public DateOnly DataFillimit { get; set; }

    public DateOnly? DataPerfundimit { get; set; }

    public string? Statusi { get; set; }

    public virtual Company Company { get; set; } = null!;

    public virtual SubscriptionPlan Plan { get; set; } = null!;
}
