using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class HighRiskPattern
{
    public int PatternId { get; set; }

    public string Pattern { get; set; } = null!;

    public bool IsEnabled { get; set; }
}
