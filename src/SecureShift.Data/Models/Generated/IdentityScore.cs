using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class IdentityScore
{
    public int IdentityScoreId { get; set; }

    public int AnalysisId { get; set; }

    public int IdentityId { get; set; }

    public int GrantedCount { get; set; }

    public int UsedCount { get; set; }

    public int UnusedHighRisk { get; set; }

    public int RiskScore { get; set; }

    public string? RiskLevel { get; set; }

    public string? SuggestedScope { get; set; }

    public virtual Analysis Analysis { get; set; } = null!;

    public virtual Identity Identity { get; set; } = null!;
}
