using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Company
{
    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = null!;

    public string? TenantId { get; set; }

    public int Subscriptions { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Analysis> Analyses { get; set; } = new List<Analysis>();

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<Identity> Identities { get; set; } = new List<Identity>();
}
