using System;
using System.Collections.Generic;

namespace SecureShift.Data.Models.Generated;

public partial class User
{
    public int UserId { get; set; }

    public string? Code { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = null!;

    public int RoleId { get; set; }

    public string? Department { get; set; }

    public string Status { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public virtual ICollection<Analysis> Analyses { get; set; } = new List<Analysis>();

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Classification> Classifications { get; set; } = new List<Classification>();

    public virtual ICollection<Company> Companies { get; set; } = new List<Company>();

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<Dependency> Dependencies { get; set; } = new List<Dependency>();

    public virtual ICollection<Enterprise> Enterprises { get; set; } = new List<Enterprise>();

    public virtual ICollection<GeneratedRole> GeneratedRoles { get; set; } = new List<GeneratedRole>();

    public virtual ICollection<User> InverseCreatedByNavigation { get; set; } = new List<User>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Plan> PlanCreatedByNavigations { get; set; } = new List<Plan>();

    public virtual ICollection<Plan> PlanDecidedByNavigations { get; set; } = new List<Plan>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<RuleSetting> RuleSettings { get; set; } = new List<RuleSetting>();

    public virtual ICollection<Simulation> Simulations { get; set; } = new List<Simulation>();
}
