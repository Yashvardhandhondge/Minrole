using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class ClassificationPolicy
{
    public int ClassificationId { get; set; }

    public string PolicyName { get; set; } = null!;

    public virtual Classification Classification { get; set; } = null!;
}
