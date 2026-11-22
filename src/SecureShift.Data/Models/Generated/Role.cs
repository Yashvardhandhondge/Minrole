using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Role
{
    public int RoleId { get; set; }

    public string RoleCode { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public string Product { get; set; } = null!;

    public string HomePage { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
