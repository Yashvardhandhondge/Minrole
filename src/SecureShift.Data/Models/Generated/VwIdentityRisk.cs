using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class VwIdentityRisk
{
    public int? AnalysisId { get; set; }

    public string? AnalysisCode { get; set; }

    public int? IdentityId { get; set; }

    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    public string? IdentityType { get; set; }

    public string? AssignedRole { get; set; }

    public string? Scope { get; set; }

    public string? ScopeName { get; set; }

    public int? GrantedCount { get; set; }

    public int? UsedCount { get; set; }

    public int? RiskScore { get; set; }

    public string? RiskLevel { get; set; }
}
