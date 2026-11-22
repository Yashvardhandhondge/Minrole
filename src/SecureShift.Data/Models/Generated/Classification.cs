using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Classification
{
    public int ClassificationId { get; set; }

    public int EnterpriseId { get; set; }

    public int TotalApps { get; set; }

    public int Dependencies { get; set; }

    public int Apis { get; set; }

    public int DatabaseCount { get; set; }

    public int StorageGb { get; set; }

    public int Environments { get; set; }

    public int BusinessUnits { get; set; }

    public int UserCount { get; set; }

    public int CritLow { get; set; }

    public int CritMedium { get; set; }

    public int CritHigh { get; set; }

    public int CritCritical { get; set; }

    public string CloudType { get; set; } = null!;

    public long LinesOfCode { get; set; }

    public decimal EmsiScore { get; set; }

    public string? Category { get; set; }

    public int? ClassifiedBy { get; set; }

    public DateOnly ClassifiedAt { get; set; }

    public virtual ICollection<ClassificationPolicy> ClassificationPolicies { get; set; } = new List<ClassificationPolicy>();

    public virtual ICollection<ClassificationTechnology> ClassificationTechnologies { get; set; } = new List<ClassificationTechnology>();

    public virtual User? ClassifiedByNavigation { get; set; }

    public virtual Enterprise Enterprise { get; set; } = null!;
}
