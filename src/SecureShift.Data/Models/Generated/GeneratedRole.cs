using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class GeneratedRole
{
    public int GeneratedRoleId { get; set; }

    public int AnalysisId { get; set; }

    public int IdentityId { get; set; }

    public string RoleName { get; set; } = null!;

    public string OriginalRole { get; set; } = null!;

    public string AssignableScope { get; set; } = null!;

    public bool IncludeBaselineRead { get; set; }

    public bool IsReviewed { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual Analysis Analysis { get; set; } = null!;

    public virtual ICollection<GeneratedRoleAction> GeneratedRoleActions { get; set; } = new List<GeneratedRoleAction>();

    public virtual Identity Identity { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
