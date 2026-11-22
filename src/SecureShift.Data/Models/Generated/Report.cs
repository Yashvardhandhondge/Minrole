using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Report
{
    public int ReportId { get; set; }

    public string Code { get; set; } = null!;

    public int AnalysisId { get; set; }

    public int IdentityCount { get; set; }

    public decimal AvgScore { get; set; }

    public int HighRiskCount { get; set; }

    public int GeneratedBy { get; set; }

    public DateTime GeneratedAt { get; set; }

    public virtual Analysis Analysis { get; set; } = null!;

    public virtual User GeneratedByNavigation { get; set; } = null!;
}
