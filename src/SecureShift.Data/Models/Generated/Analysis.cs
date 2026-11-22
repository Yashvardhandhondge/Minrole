using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Analysis
{
    public int AnalysisId { get; set; }

    public string Code { get; set; } = null!;

    public int CompanyId { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public int? PeriodDays { get; set; }

    public string Status { get; set; } = null!;

    public int? IdentityCount { get; set; }

    public decimal? AvgScore { get; set; }

    public int? HighRiskCount { get; set; }

    public int RunBy { get; set; }

    public DateTime RunAt { get; set; }

    public virtual Company Company { get; set; } = null!;

    public virtual ICollection<Finding> Findings { get; set; } = new List<Finding>();

    public virtual ICollection<GeneratedRole> GeneratedRoles { get; set; } = new List<GeneratedRole>();

    public virtual ICollection<IdentityScore> IdentityScores { get; set; } = new List<IdentityScore>();

    public virtual ICollection<PermissionUsage> PermissionUsages { get; set; } = new List<PermissionUsage>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual User RunByNavigation { get; set; } = null!;
}
