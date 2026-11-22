using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class AuditLog
{
    public int AuditLogId { get; set; }

    public int UserId { get; set; }

    public string Action { get; set; } = null!;

    public string? Target { get; set; }

    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
