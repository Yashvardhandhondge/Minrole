using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class PermissionUsage
{
    public int PermissionUsageId { get; set; }

    public int AnalysisId { get; set; }

    public int IdentityId { get; set; }

    public string Action { get; set; } = null!;

    public DateOnly FirstUsed { get; set; }

    public DateOnly LastUsed { get; set; }

    public int EventCount { get; set; }

    public string? ResourceGroup { get; set; }

    public virtual Analysis Analysis { get; set; } = null!;

    public virtual Identity Identity { get; set; } = null!;
}
