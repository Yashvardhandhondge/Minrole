using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class SimulationImpact
{
    public int SimulationImpactId { get; set; }

    public int SimulationId { get; set; }

    public int SequenceNo { get; set; }

    public string AffectedName { get; set; } = null!;

    public string AffectedType { get; set; } = null!;

    public string Criticality { get; set; } = null!;

    public string? OwnerName { get; set; }

    public virtual Simulation Simulation { get; set; } = null!;
}
