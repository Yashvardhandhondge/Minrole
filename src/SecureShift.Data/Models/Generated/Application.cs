using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class Application
{
    public int ApplicationId { get; set; }

    public string? Code { get; set; }

    public int EnterpriseId { get; set; }

    public string Name { get; set; } = null!;

    public string Technology { get; set; } = null!;

    public string Framework { get; set; } = null!;

    public string SupportStatus { get; set; } = null!;

    public int AgeYears { get; set; }

    public string Criticality { get; set; } = null!;

    public bool HasSensitiveData { get; set; }

    public int SecurityIssues { get; set; }

    public string DowntimeTolerance { get; set; } = null!;

    public int CompliancePolicies { get; set; }

    public string Deployment { get; set; } = null!;

    public string DocQuality { get; set; } = null!;

    public string Complexity { get; set; } = null!;

    public long LinesOfCode { get; set; }

    public string Status { get; set; } = null!;

    public string? BusinessUnit { get; set; }

    public string OwnerTeam { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<Component> Components { get; set; } = new List<Component>();

    public virtual ICollection<Dependency> DependencySourceApplications { get; set; } = new List<Dependency>();

    public virtual ICollection<Dependency> DependencyTargetApplications { get; set; } = new List<Dependency>();

    public virtual Enterprise Enterprise { get; set; } = null!;

    public virtual ICollection<Simulation> Simulations { get; set; } = new List<Simulation>();
}
