using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Plan
{
    public int PlanId { get; set; }

    public string? Code { get; set; }

    public int SimulationId { get; set; }

    public string PlanName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Notes { get; set; }

    public int CreatedBy { get; set; }

    public DateOnly CreatedAt { get; set; }

    public DateOnly? SubmittedAt { get; set; }

    public int? DecidedBy { get; set; }

    public DateOnly? DecidedAt { get; set; }

    public string? ExecutiveComment { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual User? DecidedByNavigation { get; set; }

    public virtual ICollection<PlanPhase> PlanPhases { get; set; } = new List<PlanPhase>();

    public virtual Simulation Simulation { get; set; } = null!;
}
