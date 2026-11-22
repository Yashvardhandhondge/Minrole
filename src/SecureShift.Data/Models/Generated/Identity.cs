using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Identity
{
    public int IdentityId { get; set; }

    public int CompanyId { get; set; }

    public string UserName { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string IdentityType { get; set; } = null!;

    public string AssignedRole { get; set; } = null!;

    public string Scope { get; set; } = null!;

    public string? ScopeName { get; set; }

    public virtual Company Company { get; set; } = null!;

    public virtual ICollection<Finding> Findings { get; set; } = new List<Finding>();

    public virtual ICollection<GeneratedRole> GeneratedRoles { get; set; } = new List<GeneratedRole>();

    public virtual ICollection<IdentityScore> IdentityScores { get; set; } = new List<IdentityScore>();

    public virtual ICollection<PermissionUsage> PermissionUsages { get; set; } = new List<PermissionUsage>();
}
