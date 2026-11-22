using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Models.Generated;

namespace SecureShift.Data.Context.Generated;

public partial class SecureShiftDbContext : DbContext
{
    public SecureShiftDbContext(DbContextOptions<SecureShiftDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Analysis> Analyses { get; set; }

    public virtual DbSet<Application> Applications { get; set; }

    public virtual DbSet<Assessment> Assessments { get; set; }

    public virtual DbSet<AssessmentFactor> AssessmentFactors { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Classification> Classifications { get; set; }

    public virtual DbSet<ClassificationPolicy> ClassificationPolicies { get; set; }

    public virtual DbSet<ClassificationTechnology> ClassificationTechnologies { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<Component> Components { get; set; }

    public virtual DbSet<Dependency> Dependencies { get; set; }

    public virtual DbSet<Enterprise> Enterprises { get; set; }

    public virtual DbSet<Finding> Findings { get; set; }

    public virtual DbSet<GeneratedRole> GeneratedRoles { get; set; }

    public virtual DbSet<GeneratedRoleAction> GeneratedRoleActions { get; set; }

    public virtual DbSet<HighRiskPattern> HighRiskPatterns { get; set; }

    public virtual DbSet<Identity> Identities { get; set; }

    public virtual DbSet<IdentityScore> IdentityScores { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PermissionUsage> PermissionUsages { get; set; }

    public virtual DbSet<Plan> Plans { get; set; }

    public virtual DbSet<PlanPhase> PlanPhases { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RuleSetting> RuleSettings { get; set; }

    public virtual DbSet<Simulation> Simulations { get; set; }

    public virtual DbSet<SimulationImpact> SimulationImpacts { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VwIdentityRisk> VwIdentityRisks { get; set; }

    public virtual DbSet<VwLatestAssessment> VwLatestAssessments { get; set; }

    public virtual DbSet<VwPlanSummary> VwPlanSummaries { get; set; }

    public virtual DbSet<VwUserLogin> VwUserLogins { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.ToTable("Analyses", "dev");

            entity.HasIndex(e => new { e.CompanyId, e.RunAt }, "IX_Analyses_Company").IsDescending(false, true);

            entity.HasIndex(e => e.Code, "UQ_Analyses_Code").IsUnique();

            entity.Property(e => e.AvgScore).HasPrecision(5, 2);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.PeriodDays).HasComputedColumnSql("((\"PeriodTo\" - \"PeriodFrom\") + 1)", true);
            entity.Property(e => e.RunAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status)
                .HasMaxLength(12)
                .HasDefaultValueSql("'Running'::character varying");

            entity.HasOne(d => d.Company).WithMany(p => p.Analyses)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Analyses_Companies");

            entity.HasOne(d => d.RunByNavigation).WithMany(p => p.Analyses)
                .HasForeignKey(d => d.RunBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Analyses_Users");
        });

        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("Applications", "dev");

            entity.HasIndex(e => new { e.EnterpriseId, e.Name }, "UQ_Apps_Name").IsUnique();

            entity.Property(e => e.BusinessUnit).HasMaxLength(100);
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('APP-'::text || \"right\"(('000'::text || ((\"ApplicationId\")::character varying(10))::text), 3))", true);
            entity.Property(e => e.Complexity).HasMaxLength(10);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Criticality).HasMaxLength(10);
            entity.Property(e => e.Deployment).HasMaxLength(20);
            entity.Property(e => e.DocQuality).HasMaxLength(10);
            entity.Property(e => e.DowntimeTolerance).HasMaxLength(10);
            entity.Property(e => e.Framework).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.OwnerTeam).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(10);
            entity.Property(e => e.SupportStatus).HasMaxLength(12);
            entity.Property(e => e.Technology).HasMaxLength(20);

            entity.HasOne(d => d.Enterprise).WithMany(p => p.Applications)
                .HasForeignKey(d => d.EnterpriseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Applications_Ent");
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments", "dev");

            entity.HasIndex(e => new { e.ApplicationId, e.AssessedAt }, "IX_Assessments_App").IsDescending(false, true);

            entity.Property(e => e.AssessedAt).HasDefaultValueSql("((CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text))::date");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('ASM-'::text || \"right\"(('000'::text || ((\"AssessmentId\")::character varying(10))::text), 3))", true);

            entity.HasOne(d => d.Application).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Assessments_App");

            entity.HasOne(d => d.AssessedByNavigation).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.AssessedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Assessments_Users");
        });

        modelBuilder.Entity<AssessmentFactor>(entity =>
        {
            entity.ToTable("AssessmentFactors", "dev");

            entity.Property(e => e.FactorName).HasMaxLength(50);
            entity.Property(e => e.FactorValue).HasMaxLength(100);
            entity.Property(e => e.ScoreType).HasMaxLength(20);

            entity.HasOne(d => d.Assessment).WithMany(p => p.AssessmentFactors)
                .HasForeignKey(d => d.AssessmentId)
                .HasConstraintName("FK_AsmFactors_Asm");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", "dev");

            entity.HasIndex(e => e.CreatedAt, "IX_AuditLogs_Created").IsDescending();

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Detail).HasMaxLength(500);
            entity.Property(e => e.Target).HasMaxLength(150);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditLogs_Users");
        });

        modelBuilder.Entity<Classification>(entity =>
        {
            entity.ToTable("Classifications", "dev");

            entity.HasIndex(e => e.EnterpriseId, "UQ_Classifications_Ent").IsUnique();

            entity.Property(e => e.Category)
                .HasMaxLength(10)
                .HasComputedColumnSql("\nCASE\n    WHEN (\"EmsiScore\" < (25)::numeric) THEN 'Foundation'::text\n    WHEN (\"EmsiScore\" < (45)::numeric) THEN 'Growth'::text\n    WHEN (\"EmsiScore\" < (70)::numeric) THEN 'Enterprise'::text\n    ELSE 'Strategic'::text\nEND", true);
            entity.Property(e => e.ClassifiedAt).HasDefaultValueSql("((CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text))::date");
            entity.Property(e => e.CloudType).HasMaxLength(20);
            entity.Property(e => e.EmsiScore).HasPrecision(5, 2);
            entity.Property(e => e.StorageGb).HasColumnName("StorageGB");

            entity.HasOne(d => d.ClassifiedByNavigation).WithMany(p => p.Classifications)
                .HasForeignKey(d => d.ClassifiedBy)
                .HasConstraintName("FK_Classifications_Users");

            entity.HasOne(d => d.Enterprise).WithOne(p => p.Classification)
                .HasForeignKey<Classification>(d => d.EnterpriseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Classifications_Ent");
        });

        modelBuilder.Entity<ClassificationPolicy>(entity =>
        {
            entity.HasKey(e => new { e.ClassificationId, e.PolicyName });

            entity.ToTable("ClassificationPolicies", "dev");

            entity.Property(e => e.PolicyName).HasMaxLength(30);

            entity.HasOne(d => d.Classification).WithMany(p => p.ClassificationPolicies)
                .HasForeignKey(d => d.ClassificationId)
                .HasConstraintName("FK_ClsPol_Cls");
        });

        modelBuilder.Entity<ClassificationTechnology>(entity =>
        {
            entity.HasKey(e => new { e.ClassificationId, e.Technology });

            entity.ToTable("ClassificationTechnologies", "dev");

            entity.Property(e => e.Technology).HasMaxLength(20);

            entity.HasOne(d => d.Classification).WithMany(p => p.ClassificationTechnologies)
                .HasForeignKey(d => d.ClassificationId)
                .HasConstraintName("FK_ClsTech_Cls");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies", "dev");

            entity.HasIndex(e => e.CompanyName, "UQ_Companies_Name").IsUnique();

            entity.Property(e => e.CompanyName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Subscriptions).HasDefaultValue(1);
            entity.Property(e => e.TenantId).HasMaxLength(36);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Companies)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Companies_Users");
        });

        modelBuilder.Entity<Component>(entity =>
        {
            entity.ToTable("Components", "dev");

            entity.HasIndex(e => new { e.EnterpriseId, e.Name }, "UQ_Components_Name").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('N-'::text || \"right\"(('00'::text || ((\"ComponentId\")::character varying(10))::text), 2))", true);
            entity.Property(e => e.ComponentType).HasMaxLength(10);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.Application).WithMany(p => p.Components)
                .HasForeignKey(d => d.ApplicationId)
                .HasConstraintName("FK_Components_App");

            entity.HasOne(d => d.Enterprise).WithMany(p => p.Components)
                .HasForeignKey(d => d.EnterpriseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Components_Ent");
        });

        modelBuilder.Entity<Dependency>(entity =>
        {
            entity.ToTable("Dependencies", "dev");

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('DEP-'::text || \"right\"(('000'::text || ((\"DependencyId\")::character varying(10))::text), 3))", true);
            entity.Property(e => e.Criticality).HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(200);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Dependencies)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Deps_Users");

            entity.HasOne(d => d.Enterprise).WithMany(p => p.Dependencies)
                .HasForeignKey(d => d.EnterpriseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Deps_Ent");

            entity.HasOne(d => d.SourceApplication).WithMany(p => p.DependencySourceApplications)
                .HasForeignKey(d => d.SourceApplicationId)
                .HasConstraintName("FK_Deps_SrcApp");

            entity.HasOne(d => d.SourceComponent).WithMany(p => p.DependencySourceComponents)
                .HasForeignKey(d => d.SourceComponentId)
                .HasConstraintName("FK_Deps_SrcComp");

            entity.HasOne(d => d.TargetApplication).WithMany(p => p.DependencyTargetApplications)
                .HasForeignKey(d => d.TargetApplicationId)
                .HasConstraintName("FK_Deps_TgtApp");

            entity.HasOne(d => d.TargetComponent).WithMany(p => p.DependencyTargetComponents)
                .HasForeignKey(d => d.TargetComponentId)
                .HasConstraintName("FK_Deps_TgtComp");
        });

        modelBuilder.Entity<Enterprise>(entity =>
        {
            entity.ToTable("Enterprises", "dev");

            entity.HasIndex(e => e.Name, "UQ_Enterprises_Name").IsUnique();

            entity.Property(e => e.City).HasMaxLength(50);
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('ENT-'::text || \"right\"(('000'::text || ((\"EnterpriseId\")::character varying(10))::text), 3))", true);
            entity.Property(e => e.ContactEmail).HasMaxLength(150);
            entity.Property(e => e.ContactName).HasMaxLength(50);
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.Industry).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.RegisteredAt).HasDefaultValueSql("((CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text))::date");

            entity.HasOne(d => d.RegisteredByNavigation).WithMany(p => p.Enterprises)
                .HasForeignKey(d => d.RegisteredBy)
                .HasConstraintName("FK_Enterprises_Users");
        });

        modelBuilder.Entity<Finding>(entity =>
        {
            entity.ToTable("Findings", "dev");

            entity.HasIndex(e => new { e.AnalysisId, e.Category }, "IX_Findings_Analysis");

            entity.Property(e => e.Action).HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(20);
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('F-'::text || ((\"FindingId\")::character varying(10))::text)", true);
            entity.Property(e => e.GrantedByRole).HasMaxLength(50);
            entity.Property(e => e.Severity).HasMaxLength(10);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValueSql("'Open'::character varying");

            entity.HasOne(d => d.Analysis).WithMany(p => p.Findings)
                .HasForeignKey(d => d.AnalysisId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Findings_Analyses");

            entity.HasOne(d => d.Identity).WithMany(p => p.Findings)
                .HasForeignKey(d => d.IdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Findings_Identities");
        });

        modelBuilder.Entity<GeneratedRole>(entity =>
        {
            entity.ToTable("GeneratedRoles", "dev");

            entity.HasIndex(e => new { e.AnalysisId, e.IdentityId }, "UQ_GenRoles").IsUnique();

            entity.Property(e => e.AssignableScope).HasMaxLength(100);
            entity.Property(e => e.IncludeBaselineRead).HasDefaultValue(true);
            entity.Property(e => e.IsReviewed).HasDefaultValue(false);
            entity.Property(e => e.OriginalRole).HasMaxLength(100);
            entity.Property(e => e.RoleName).HasMaxLength(150);

            entity.HasOne(d => d.Analysis).WithMany(p => p.GeneratedRoles)
                .HasForeignKey(d => d.AnalysisId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GenRoles_Analyses");

            entity.HasOne(d => d.Identity).WithMany(p => p.GeneratedRoles)
                .HasForeignKey(d => d.IdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GenRoles_Identities");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.GeneratedRoles)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_GenRoles_Users");
        });

        modelBuilder.Entity<GeneratedRoleAction>(entity =>
        {
            entity.HasKey(e => e.GeneratedRoleActionId).HasName("PK_GenRoleActions");

            entity.ToTable("GeneratedRoleActions", "dev");

            entity.HasIndex(e => new { e.GeneratedRoleId, e.Action }, "UQ_GenRoleActions").IsUnique();

            entity.Property(e => e.Action).HasMaxLength(200);
            entity.Property(e => e.IsRare).HasDefaultValue(false);
            entity.Property(e => e.Note).HasMaxLength(200);

            entity.HasOne(d => d.GeneratedRole).WithMany(p => p.GeneratedRoleActions)
                .HasForeignKey(d => d.GeneratedRoleId)
                .HasConstraintName("FK_GenRoleActions_Roles");
        });

        modelBuilder.Entity<HighRiskPattern>(entity =>
        {
            entity.HasKey(e => e.PatternId);

            entity.ToTable("HighRiskPatterns", "dev");

            entity.HasIndex(e => e.Pattern, "UQ_Patterns").IsUnique();

            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Pattern).HasMaxLength(200);
        });

        modelBuilder.Entity<Identity>(entity =>
        {
            entity.ToTable("Identities", "dev");

            entity.HasIndex(e => new { e.CompanyId, e.UserName }, "UQ_Identities_UserName").IsUnique();

            entity.Property(e => e.AssignedRole).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.IdentityType).HasMaxLength(20);
            entity.Property(e => e.Scope).HasMaxLength(30);
            entity.Property(e => e.ScopeName).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.Company).WithMany(p => p.Identities)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Identities_Companies");
        });

        modelBuilder.Entity<IdentityScore>(entity =>
        {
            entity.ToTable("IdentityScores", "dev");

            entity.HasIndex(e => new { e.AnalysisId, e.IdentityId }, "UQ_IdentityScores").IsUnique();

            entity.Property(e => e.RiskLevel)
                .HasMaxLength(6)
                .HasComputedColumnSql("\nCASE\n    WHEN (\"RiskScore\" >= 70) THEN 'HIGH'::text\n    WHEN (\"RiskScore\" >= 40) THEN 'MEDIUM'::text\n    ELSE 'LOW'::text\nEND", true);
            entity.Property(e => e.SuggestedScope).HasMaxLength(100);
            entity.Property(e => e.UnusedHighRisk).HasDefaultValue(0);

            entity.HasOne(d => d.Analysis).WithMany(p => p.IdentityScores)
                .HasForeignKey(d => d.AnalysisId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_IdentityScores_Analyses");

            entity.HasOne(d => d.Identity).WithMany(p => p.IdentityScores)
                .HasForeignKey(d => d.IdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_IdentityScores_Identities");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications", "dev");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Message).HasMaxLength(300);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<PermissionUsage>(entity =>
        {
            entity.ToTable("PermissionUsage", "dev");

            entity.HasIndex(e => new { e.AnalysisId, e.IdentityId }, "IX_PermUsage_Identity");

            entity.Property(e => e.Action).HasMaxLength(200);
            entity.Property(e => e.ResourceGroup).HasMaxLength(100);

            entity.HasOne(d => d.Analysis).WithMany(p => p.PermissionUsages)
                .HasForeignKey(d => d.AnalysisId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PermUsage_Analyses");

            entity.HasOne(d => d.Identity).WithMany(p => p.PermissionUsages)
                .HasForeignKey(d => d.IdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PermUsage_Identities");
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans", "dev");

            entity.HasIndex(e => e.PlanName, "UQ_Plans_Name").IsUnique();

            entity.HasIndex(e => e.SimulationId, "UQ_Plans_Sim").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('PLAN-'::text || \"right\"(('000'::text || ((\"PlanId\")::character varying(10))::text), 3))", true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("((CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text))::date");
            entity.Property(e => e.ExecutiveComment).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.PlanName).HasMaxLength(150);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValueSql("'Draft'::character varying");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PlanCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Plans_CreatedBy");

            entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.PlanDecidedByNavigations)
                .HasForeignKey(d => d.DecidedBy)
                .HasConstraintName("FK_Plans_DecidedBy");

            entity.HasOne(d => d.Simulation).WithOne(p => p.Plan)
                .HasForeignKey<Plan>(d => d.SimulationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Plans_Sim");
        });

        modelBuilder.Entity<PlanPhase>(entity =>
        {
            entity.ToTable("PlanPhases", "dev");

            entity.HasIndex(e => new { e.PlanId, e.PhaseNo }, "UQ_PlanPhases").IsUnique();

            entity.Property(e => e.PhaseName).HasMaxLength(100);

            entity.HasOne(d => d.Plan).WithMany(p => p.PlanPhases)
                .HasForeignKey(d => d.PlanId)
                .HasConstraintName("FK_PlanPhases_Plans");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("Reports", "dev");

            entity.HasIndex(e => e.Code, "UQ_Reports_Code").IsUnique();

            entity.Property(e => e.AvgScore).HasPrecision(5, 2);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Analysis).WithMany(p => p.Reports)
                .HasForeignKey(d => d.AnalysisId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Analyses");

            entity.HasOne(d => d.GeneratedByNavigation).WithMany(p => p.Reports)
                .HasForeignKey(d => d.GeneratedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Users");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles", "dev");

            entity.HasIndex(e => e.RoleCode, "UQ_Roles_Code").IsUnique();

            entity.HasIndex(e => e.RoleName, "UQ_Roles_Name").IsUnique();

            entity.Property(e => e.HomePage).HasMaxLength(100);
            entity.Property(e => e.Product).HasMaxLength(20);
            entity.Property(e => e.RoleCode).HasMaxLength(20);
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<RuleSetting>(entity =>
        {
            entity.ToTable("RuleSettings", "dev");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RuleSettings)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_Rules_Users");
        });

        modelBuilder.Entity<Simulation>(entity =>
        {
            entity.ToTable("Simulations", "dev");

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('SIM-'::text || \"right\"(('000'::text || ((\"SimulationId\")::character varying(10))::text), 3))", true);
            entity.Property(e => e.CostLakh).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("((CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text))::date");
            entity.Property(e => e.EmsiScore).HasPrecision(5, 2);
            entity.Property(e => e.Recommendation).HasMaxLength(500);
            entity.Property(e => e.RiskLevel)
                .HasMaxLength(6)
                .HasComputedColumnSql("\nCASE\n    WHEN (\"ModernizationRisk\" >= 70) THEN 'High'::text\n    WHEN (\"ModernizationRisk\" >= 40) THEN 'Medium'::text\n    ELSE 'Low'::text\nEND", true);
            entity.Property(e => e.Strategy).HasMaxLength(12);

            entity.HasOne(d => d.Application).WithMany(p => p.Simulations)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Simulations_App");

            entity.HasOne(d => d.Assessment).WithMany(p => p.Simulations)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Simulations_Asm");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Simulations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Simulations_Users");
        });

        modelBuilder.Entity<SimulationImpact>(entity =>
        {
            entity.ToTable("SimulationImpacts", "dev");

            entity.Property(e => e.AffectedName).HasMaxLength(100);
            entity.Property(e => e.AffectedType).HasMaxLength(12);
            entity.Property(e => e.Criticality).HasMaxLength(10);
            entity.Property(e => e.OwnerName).HasMaxLength(100);

            entity.HasOne(d => d.Simulation).WithMany(p => p.SimulationImpacts)
                .HasForeignKey(d => d.SimulationId)
                .HasConstraintName("FK_SimImpacts_Sim");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "dev");

            entity.HasIndex(e => e.RoleId, "IX_Users_Role");

            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComputedColumnSql("('USR-'::text || \"right\"(('000'::text || ((\"UserId\")::character varying(10))::text), 3))", true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Department).HasMaxLength(150);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(50);
            entity.Property(e => e.MustChangePassword).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Phone)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValueSql("'Active'::character varying");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Users_CreatedBy");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<VwIdentityRisk>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_IdentityRisk", "dev");

            entity.Property(e => e.AnalysisCode).HasMaxLength(20);
            entity.Property(e => e.AssignedRole).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.IdentityType).HasMaxLength(20);
            entity.Property(e => e.RiskLevel).HasMaxLength(6);
            entity.Property(e => e.Scope).HasMaxLength(30);
            entity.Property(e => e.ScopeName).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);
        });

        modelBuilder.Entity<VwLatestAssessment>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_LatestAssessment", "dev");

            entity.Property(e => e.Code).HasMaxLength(20);
        });

        modelBuilder.Entity<VwPlanSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PlanSummary", "dev");

            entity.Property(e => e.ApplicationCode).HasMaxLength(20);
            entity.Property(e => e.ApplicationName).HasMaxLength(100);
            entity.Property(e => e.CostLakh).HasPrecision(10, 2);
            entity.Property(e => e.CreatedByName).HasMaxLength(50);
            entity.Property(e => e.DecidedByName).HasMaxLength(50);
            entity.Property(e => e.EnterpriseCode).HasMaxLength(20);
            entity.Property(e => e.EnterpriseName).HasMaxLength(150);
            entity.Property(e => e.ExecutiveComment).HasMaxLength(500);
            entity.Property(e => e.PlanCode).HasMaxLength(20);
            entity.Property(e => e.PlanName).HasMaxLength(150);
            entity.Property(e => e.RiskLevel).HasMaxLength(6);
            entity.Property(e => e.SimulationCode).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(10);
            entity.Property(e => e.Strategy).HasMaxLength(12);
        });

        modelBuilder.Entity<VwUserLogin>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_UserLogin", "dev");

            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(50);
            entity.Property(e => e.HomePage).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Product).HasMaxLength(20);
            entity.Property(e => e.RoleCode).HasMaxLength(20);
            entity.Property(e => e.RoleName).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(10);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
