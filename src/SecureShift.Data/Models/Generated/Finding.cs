using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Finding
{
    public int FindingId { get; set; }

    public string? Code { get; set; }

    public int AnalysisId { get; set; }

    public int IdentityId { get; set; }

    public string Action { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public string GrantedByRole { get; set; } = null!;

    public DateOnly? LastUsed { get; set; }

    public string Status { get; set; } = null!;

    public virtual Analysis Analysis { get; set; } = null!;

    public virtual Identity Identity { get; set; } = null!;
}
