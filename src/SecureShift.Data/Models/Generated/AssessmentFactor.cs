using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class AssessmentFactor
{
    public int AssessmentFactorId { get; set; }

    public int AssessmentId { get; set; }

    public string ScoreType { get; set; } = null!;

    public string FactorName { get; set; } = null!;

    public string FactorValue { get; set; } = null!;

    public int Points { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;
}
