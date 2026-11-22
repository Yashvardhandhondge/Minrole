using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class ClassificationTechnology
{
    public int ClassificationId { get; set; }

    public string Technology { get; set; } = null!;

    public virtual Classification Classification { get; set; } = null!;
}
