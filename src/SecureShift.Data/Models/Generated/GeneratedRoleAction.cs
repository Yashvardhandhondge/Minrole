using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class GeneratedRoleAction
{
    public int GeneratedRoleActionId { get; set; }

    public int GeneratedRoleId { get; set; }

    public string Action { get; set; } = null!;

    public bool IsRare { get; set; }

    public string? Note { get; set; }

    public virtual GeneratedRole GeneratedRole { get; set; } = null!;
}
