using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Dependency
{
    public int DependencyId { get; set; }

    public string? Code { get; set; }

    public int EnterpriseId { get; set; }

    public int? SourceApplicationId { get; set; }

    public int? SourceComponentId { get; set; }

    public int? TargetApplicationId { get; set; }

    public int? TargetComponentId { get; set; }

    public string Criticality { get; set; } = null!;

    public string? Description { get; set; }

    public int? CreatedBy { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Enterprise Enterprise { get; set; } = null!;

    public virtual Application? SourceApplication { get; set; }

    public virtual Component? SourceComponent { get; set; }

    public virtual Application? TargetApplication { get; set; }

    public virtual Component? TargetComponent { get; set; }
}
