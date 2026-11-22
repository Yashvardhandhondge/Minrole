using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class VwPlanSummary
{
    public int? PlanId { get; set; }

    public string? PlanCode { get; set; }

    public string? PlanName { get; set; }

    public string? Status { get; set; }

    public DateOnly? CreatedAt { get; set; }

    public DateOnly? SubmittedAt { get; set; }

    public DateOnly? DecidedAt { get; set; }

    public string? SimulationCode { get; set; }

    public string? Strategy { get; set; }

    public int? ModernizationRisk { get; set; }

    public string? RiskLevel { get; set; }

    public int? EffortWeeks { get; set; }

    public int? TeamSize { get; set; }

    public decimal? CostLakh { get; set; }

    public string? ApplicationCode { get; set; }

    public string? ApplicationName { get; set; }

    public string? EnterpriseCode { get; set; }

    public string? EnterpriseName { get; set; }

    public string? CreatedByName { get; set; }

    public string? DecidedByName { get; set; }

    public string? ExecutiveComment { get; set; }
}
