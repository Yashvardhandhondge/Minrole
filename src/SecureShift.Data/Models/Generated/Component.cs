using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Component
{
    public int ComponentId { get; set; }

    public string? Code { get; set; }

    public int EnterpriseId { get; set; }

    public int? ApplicationId { get; set; }

    public string ComponentType { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual Application? Application { get; set; }

    public virtual ICollection<Dependency> DependencySourceComponents { get; set; } = new List<Dependency>();

    public virtual ICollection<Dependency> DependencyTargetComponents { get; set; } = new List<Dependency>();

    public virtual Enterprise Enterprise { get; set; } = null!;
}
