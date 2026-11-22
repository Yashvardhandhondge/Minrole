using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class VwLatestAssessment
{
    public int? AssessmentId { get; set; }

    public string? Code { get; set; }

    public int? ApplicationId { get; set; }

    public int? TechnicalDebt { get; set; }

    public int? GovernanceRisk { get; set; }

    public int? DependencyImpact { get; set; }

    public int? AssessedBy { get; set; }

    public DateOnly? AssessedAt { get; set; }
}
