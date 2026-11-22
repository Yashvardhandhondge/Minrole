using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Simulation
{
    public int SimulationId { get; set; }

    public string? Code { get; set; }

    public int ApplicationId { get; set; }

    public int AssessmentId { get; set; }

    public string Strategy { get; set; } = null!;

    public int StrategyRisk { get; set; }

    public decimal? EmsiScore { get; set; }

    public int ModernizationRisk { get; set; }

    public string? RiskLevel { get; set; }

    public int EffortWeeks { get; set; }

    public int TeamSize { get; set; }

    public decimal CostLakh { get; set; }

    public string? Recommendation { get; set; }

    public int CreatedBy { get; set; }

    public DateOnly CreatedAt { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Plan? Plan { get; set; }

    public virtual ICollection<SimulationImpact> SimulationImpacts { get; set; } = new List<SimulationImpact>();
}
