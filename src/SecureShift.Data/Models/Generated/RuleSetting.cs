using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class RuleSetting
{
    public int RuleSettingId { get; set; }

    public int WeightUnused { get; set; }

    public int WeightHighRisk { get; set; }

    public int WeightScope { get; set; }

    public int DefaultPeriodDays { get; set; }

    public int RareThreshold { get; set; }

    public bool IncludeBaselineRead { get; set; }

    public bool IsActive { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }
}
