using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Assessment
{
    public int AssessmentId { get; set; }

    public string? Code { get; set; }

    public int ApplicationId { get; set; }

    public int TechnicalDebt { get; set; }

    public int GovernanceRisk { get; set; }

    public int DependencyImpact { get; set; }

    public int AssessedBy { get; set; }

    public DateOnly AssessedAt { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual User AssessedByNavigation { get; set; } = null!;

    public virtual ICollection<AssessmentFactor> AssessmentFactors { get; set; } = new List<AssessmentFactor>();

    public virtual ICollection<Simulation> Simulations { get; set; } = new List<Simulation>();
}
