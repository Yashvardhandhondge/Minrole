using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class PlanPhase
{
    public int PlanPhaseId { get; set; }

    public int PlanId { get; set; }

    public int PhaseNo { get; set; }

    public string PhaseName { get; set; } = null!;

    public int Weeks { get; set; }

    public virtual Plan Plan { get; set; } = null!;
}
