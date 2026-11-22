using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Enterprise
{
    public int EnterpriseId { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = null!;

    public string Industry { get; set; } = null!;

    public string? City { get; set; }

    public string ContactName { get; set; } = null!;

    public string ContactEmail { get; set; } = null!;

    public string? ContactPhone { get; set; }

    public int? RegisteredBy { get; set; }

    public DateOnly RegisteredAt { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual Classification? Classification { get; set; }

    public virtual ICollection<Component> Components { get; set; } = new List<Component>();

    public virtual ICollection<Dependency> Dependencies { get; set; } = new List<Dependency>();

    public virtual User? RegisteredByNavigation { get; set; }
}
