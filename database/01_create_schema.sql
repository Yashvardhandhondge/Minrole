/* =====================================================================
   SecureShift - 01_create_schema.sql
   Phase 2: Database   (SQL Server 2019+ / Azure SQL Database)

   Creates the SecureShiftDB database and all tables for:
     auth  : users, roles, audit logs                     (shared by both modules)
     gov   : Cloud Governance (least-privilege analysis)
     emdt  : Enterprise Modernization Digital Twin (Modernization)

   How to run (SSMS or Azure Data Studio):
     1. Open this file and press Execute (F5).
     2. Then run 02_seed_data.sql to load the sample data.
     3. Optional: 03_sample_queries.sql shows the queries the screens need.

   On Azure SQL Database (not a full SQL Server):
     Azure does not allow CREATE DATABASE / USE inside a script.
     Create the database "SecureShiftDB" in the Azure portal first, connect to it,
     then run this file starting from section 2 (skip section 1), and remove
     the "USE SecureShiftDB;" line at the top of 02_seed_data.sql.

   Naming rules used everywhere:
     - Table names are plural (Users, Applications).
     - Every table has an INT IDENTITY primary key called <Table>Id.
     - Tables the UI shows with a code (APP-001, ENT-001 ...) have a
       computed column Code, built from the Id. Example: ApplicationId 1 -> 'APP-001'.
     - CHECK constraints hold the same rules as the prototype's validations.
   ===================================================================== */

/* ---------- 1. Database ---------- */
IF DB_ID(N'SecureShiftDB') IS NULL
    CREATE DATABASE SecureShiftDB;
GO
USE SecureShiftDB;
GO

/* ---------- 2. Drop old tables (so the script can be run again) ----------
   Children first, parents last. */
DROP TABLE IF EXISTS emdt.PlanPhases;
DROP TABLE IF EXISTS emdt.Plans;
DROP TABLE IF EXISTS emdt.SimulationImpacts;
DROP TABLE IF EXISTS emdt.Simulations;
DROP TABLE IF EXISTS emdt.AssessmentFactors;
DROP TABLE IF EXISTS emdt.Assessments;
DROP TABLE IF EXISTS emdt.Dependencies;
DROP TABLE IF EXISTS emdt.Components;
DROP TABLE IF EXISTS emdt.Applications;
DROP TABLE IF EXISTS emdt.ClassificationPolicies;
DROP TABLE IF EXISTS emdt.ClassificationTechnologies;
DROP TABLE IF EXISTS emdt.Classifications;
DROP TABLE IF EXISTS emdt.Enterprises;

DROP TABLE IF EXISTS gov.Notifications;
DROP TABLE IF EXISTS gov.Reports;
DROP TABLE IF EXISTS gov.GeneratedRoleActions;
DROP TABLE IF EXISTS gov.GeneratedRoles;
DROP TABLE IF EXISTS gov.Findings;
DROP TABLE IF EXISTS gov.PermissionUsage;
DROP TABLE IF EXISTS gov.IdentityScores;
DROP TABLE IF EXISTS gov.Identities;
DROP TABLE IF EXISTS gov.Analyses;
DROP TABLE IF EXISTS gov.HighRiskPatterns;
DROP TABLE IF EXISTS gov.RuleSettings;
DROP TABLE IF EXISTS gov.Companies;

DROP TABLE IF EXISTS auth.EmailNotifications;   -- removed in v7 (no email sending); kept here so old copies are cleaned up
DROP TABLE IF EXISTS auth.AuditLogs;
DROP TABLE IF EXISTS auth.Users;
DROP TABLE IF EXISTS auth.Roles;
GO

/* ---------- 3. Schemas ---------- */
IF SCHEMA_ID(N'auth') IS NULL EXEC (N'CREATE SCHEMA auth');
IF SCHEMA_ID(N'gov')  IS NULL EXEC (N'CREATE SCHEMA gov');
IF SCHEMA_ID(N'emdt') IS NULL EXEC (N'CREATE SCHEMA emdt');
GO

/* =====================================================================
   AUTH  (shared)
   ===================================================================== */

-- The 5 roles. Product says which module the role belongs to.
CREATE TABLE auth.Roles (
    RoleId        INT IDENTITY(1,1) CONSTRAINT PK_Roles PRIMARY KEY,
    RoleCode      VARCHAR(20)  NOT NULL,   -- cloudadmin, superadmin ...
    RoleName      VARCHAR(50)  NOT NULL,   -- Cloud Admin, Super Admin ...
    Product       VARCHAR(20)  NOT NULL CONSTRAINT CK_Roles_Product CHECK (Product IN ('Governance', 'Modernization')),
    HomePage      VARCHAR(100) NOT NULL,   -- dashboard opened after login
    CONSTRAINT UQ_Roles_Code UNIQUE (RoleCode),
    CONSTRAINT UQ_Roles_Name UNIQUE (RoleName)
);

-- Everyone who can log in (both modules).
CREATE TABLE auth.Users (
    UserId        INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    Code          AS ('USR-' + RIGHT('000' + CAST(UserId AS VARCHAR(10)), 3)) PERSISTED,
    FullName      VARCHAR(50)  NOT NULL CONSTRAINT CK_Users_Name  CHECK (LEN(FullName) >= 3 AND FullName NOT LIKE '%[^A-Za-z ]%'),
    Email         VARCHAR(150) NOT NULL
                               CONSTRAINT CK_Users_Email CHECK (Email LIKE '_%@_%._%'),
    Phone         CHAR(10)     NULL     CONSTRAINT CK_Users_Phone CHECK (Phone LIKE '[6-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    PasswordHash  VARCHAR(256) NOT NULL,                 -- never store the plain password
    RoleId        INT          NOT NULL CONSTRAINT FK_Users_Roles REFERENCES auth.Roles (RoleId),
    Department    VARCHAR(150) NULL     CONSTRAINT CK_Users_Dept  CHECK (Department IS NULL OR (LEN(Department) >= 3 AND Department NOT LIKE '%[^A-Za-z0-9 ]%')),
    Status        VARCHAR(10)  NOT NULL CONSTRAINT DF_Users_Status DEFAULT ('Active')   -- new users are always Active
                               CONSTRAINT CK_Users_Status CHECK (Status IN ('Active', 'Inactive')),
    MustChangePassword BIT     NOT NULL CONSTRAINT DF_Users_MustChange DEFAULT (1),
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Users_Created DEFAULT (SYSUTCDATETIME()),
    CreatedBy     INT          NULL     CONSTRAINT FK_Users_CreatedBy REFERENCES auth.Users (UserId),
    LastLoginAt   DATETIME2(0) NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
CREATE INDEX IX_Users_Role ON auth.Users (RoleId);

-- Every important action (Super Admin > Audit Logs page).
CREATE TABLE auth.AuditLogs (
    AuditLogId    INT IDENTITY(1,1) CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId        INT          NOT NULL CONSTRAINT FK_AuditLogs_Users REFERENCES auth.Users (UserId),  -- who did it
    Action        VARCHAR(50)  NOT NULL,     -- User Created, Role Changed, Plan Approved ...
    Target        VARCHAR(150) NULL,         -- what it was done to
    Detail        VARCHAR(500) NULL,
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLogs_Created DEFAULT (SYSUTCDATETIME())
);
CREATE INDEX IX_AuditLogs_Created ON auth.AuditLogs (CreatedAt DESC);

GO

/* =====================================================================
   GOV  - Cloud Governance
   Flow: Company -> Analysis (fixed period) -> Identity scores, Findings,
         Generated roles -> Report
   ===================================================================== */

-- Each Azure tenant (customer company) we analyse.
CREATE TABLE gov.Companies (
    CompanyId     INT IDENTITY(1,1) CONSTRAINT PK_Companies PRIMARY KEY,
    CompanyName   VARCHAR(100) NOT NULL CONSTRAINT CK_Companies_Name CHECK (LEN(CompanyName) >= 3),
    TenantId      VARCHAR(36)  NULL,       -- Azure AD tenant GUID
    Subscriptions INT          NOT NULL CONSTRAINT DF_Companies_Subs DEFAULT (1) CONSTRAINT CK_Companies_Subs CHECK (Subscriptions BETWEEN 1 AND 500),
    CreatedBy     INT          NULL     CONSTRAINT FK_Companies_Users REFERENCES auth.Users (UserId),
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Companies_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Companies_Name UNIQUE (CompanyName)
);

-- One run of the 4-step Analysis Wizard, always on a fixed period (7-180 days).
CREATE TABLE gov.Analyses (
    AnalysisId    INT IDENTITY(1,1) CONSTRAINT PK_Analyses PRIMARY KEY,
    Code          VARCHAR(20)  NOT NULL,   -- AN-2026-1110
    CompanyId     INT          NOT NULL CONSTRAINT FK_Analyses_Companies REFERENCES gov.Companies (CompanyId),
    PeriodFrom    DATE         NOT NULL,
    PeriodTo      DATE         NOT NULL,
    PeriodDays    AS (DATEDIFF(DAY, PeriodFrom, PeriodTo) + 1),
    Status        VARCHAR(12)  NOT NULL CONSTRAINT DF_Analyses_Status DEFAULT ('Running')
                               CONSTRAINT CK_Analyses_Status CHECK (Status IN ('Running', 'Complete', 'Failed')),
    IdentityCount INT          NULL,
    AvgScore      DECIMAL(5,2) NULL,
    HighRiskCount INT          NULL,
    RunBy         INT          NOT NULL CONSTRAINT FK_Analyses_Users REFERENCES auth.Users (UserId),
    RunAt         DATETIME2(0) NOT NULL CONSTRAINT DF_Analyses_RunAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Analyses_Period CHECK (DATEDIFF(DAY, PeriodFrom, PeriodTo) + 1 BETWEEN 7 AND 180),
    CONSTRAINT UQ_Analyses_Code UNIQUE (Code)
);
CREATE INDEX IX_Analyses_Company ON gov.Analyses (CompanyId, RunAt DESC);

-- Azure identities: users, service principals, managed identities.
CREATE TABLE gov.Identities (
    IdentityId    INT IDENTITY(1,1) CONSTRAINT PK_Identities PRIMARY KEY,
    CompanyId     INT          NOT NULL CONSTRAINT FK_Identities_Companies REFERENCES gov.Companies (CompanyId),
    UserName      VARCHAR(100) NOT NULL,      -- rahul.verma, svc-webapp-deploy
    DisplayName   VARCHAR(100) NOT NULL,      -- Rahul Verma (shown on every screen)
    IdentityType  VARCHAR(20)  NOT NULL CONSTRAINT CK_Identities_Type CHECK (IdentityType IN ('User', 'Service Principal', 'Managed Identity')),
    AssignedRole  VARCHAR(50)  NOT NULL,      -- Owner, Contributor, Reader ...
    Scope         VARCHAR(30)  NOT NULL CONSTRAINT CK_Identities_Scope CHECK (Scope IN ('Management Group', 'Subscription', 'Resource Group', 'Resource')),
    ScopeName     VARCHAR(100) NULL,         -- rg-sales (NULL when the scope is the whole subscription)
    CONSTRAINT UQ_Identities_UserName UNIQUE (CompanyId, UserName)
);

-- The score of one identity in one analysis (scores change from run to run).
CREATE TABLE gov.IdentityScores (
    IdentityScoreId   INT IDENTITY(1,1) CONSTRAINT PK_IdentityScores PRIMARY KEY,
    AnalysisId        INT NOT NULL CONSTRAINT FK_IdentityScores_Analyses REFERENCES gov.Analyses (AnalysisId),
    IdentityId        INT NOT NULL CONSTRAINT FK_IdentityScores_Identities REFERENCES gov.Identities (IdentityId),
    GrantedCount      INT NOT NULL CONSTRAINT CK_IdentityScores_Granted CHECK (GrantedCount >= 0),
    UsedCount         INT NOT NULL CONSTRAINT CK_IdentityScores_Used    CHECK (UsedCount >= 0),
    UnusedHighRisk    INT NOT NULL CONSTRAINT DF_IdentityScores_UHR DEFAULT (0),
    RiskScore         INT NOT NULL CONSTRAINT CK_IdentityScores_Score   CHECK (RiskScore BETWEEN 0 AND 100),
    RiskLevel         AS (CASE WHEN RiskScore >= 70 THEN 'HIGH' WHEN RiskScore >= 40 THEN 'MEDIUM' ELSE 'LOW' END),
    SuggestedScope    VARCHAR(100) NULL,       -- rg-webapp-prod
    CONSTRAINT UQ_IdentityScores UNIQUE (AnalysisId, IdentityId),
    CONSTRAINT CK_IdentityScores_UsedLeGranted CHECK (UsedCount <= GrantedCount)
);

-- Evidence: which permissions an identity really used in the period.
CREATE TABLE gov.PermissionUsage (
    PermissionUsageId INT IDENTITY(1,1) CONSTRAINT PK_PermissionUsage PRIMARY KEY,
    AnalysisId        INT          NOT NULL CONSTRAINT FK_PermUsage_Analyses REFERENCES gov.Analyses (AnalysisId),
    IdentityId        INT          NOT NULL CONSTRAINT FK_PermUsage_Identities REFERENCES gov.Identities (IdentityId),
    Action            VARCHAR(200) NOT NULL,   -- Microsoft.Web/sites/write
    FirstUsed         DATE         NOT NULL,
    LastUsed          DATE         NOT NULL,
    EventCount        INT          NOT NULL CONSTRAINT CK_PermUsage_Events CHECK (EventCount > 0),
    ResourceGroup     VARCHAR(100) NULL
);
CREATE INDEX IX_PermUsage_Identity ON gov.PermissionUsage (AnalysisId, IdentityId);

-- High-risk permissions that were granted but never used (read-only, no Revoke).
CREATE TABLE gov.Findings (
    FindingId     INT IDENTITY(1,1) CONSTRAINT PK_Findings PRIMARY KEY,
    Code          AS ('F-' + CAST(FindingId AS VARCHAR(10))) PERSISTED,
    AnalysisId    INT          NOT NULL CONSTRAINT FK_Findings_Analyses REFERENCES gov.Analyses (AnalysisId),
    IdentityId    INT          NOT NULL CONSTRAINT FK_Findings_Identities REFERENCES gov.Identities (IdentityId),
    Action        VARCHAR(200) NOT NULL,
    Category      VARCHAR(20)  NOT NULL CONSTRAINT CK_Findings_Category CHECK (Category IN ('Delete', 'Network', 'Key Vault', 'Role Assignment')),
    Severity      VARCHAR(10)  NOT NULL CONSTRAINT CK_Findings_Severity CHECK (Severity IN ('HIGH', 'MEDIUM', 'LOW')),
    GrantedByRole VARCHAR(50)  NOT NULL,
    LastUsed      DATE         NULL,          -- NULL = never used
    Status        VARCHAR(10)  NOT NULL CONSTRAINT DF_Findings_Status DEFAULT ('Open')
                               CONSTRAINT CK_Findings_Status CHECK (Status IN ('Open', 'Resolved'))
);
CREATE INDEX IX_Findings_Analysis ON gov.Findings (AnalysisId, Category);

-- Least-privilege custom role generated for one identity.
CREATE TABLE gov.GeneratedRoles (
    GeneratedRoleId INT IDENTITY(1,1) CONSTRAINT PK_GeneratedRoles PRIMARY KEY,
    AnalysisId      INT          NOT NULL CONSTRAINT FK_GenRoles_Analyses REFERENCES gov.Analyses (AnalysisId),
    IdentityId      INT          NOT NULL CONSTRAINT FK_GenRoles_Identities REFERENCES gov.Identities (IdentityId),
    RoleName        VARCHAR(150) NOT NULL,      -- svc-webapp-deploy-custom-role
    OriginalRole    VARCHAR(100) NOT NULL,      -- Contributor (Subscription)
    AssignableScope VARCHAR(100) NOT NULL,      -- rg-webapp-prod
    IncludeBaselineRead BIT      NOT NULL CONSTRAINT DF_GenRoles_Baseline DEFAULT (1),
    IsReviewed      BIT          NOT NULL CONSTRAINT DF_GenRoles_Reviewed DEFAULT (0),
    ReviewedBy      INT          NULL     CONSTRAINT FK_GenRoles_Users REFERENCES auth.Users (UserId),
    ReviewedAt      DATETIME2(0) NULL,
    CONSTRAINT UQ_GenRoles UNIQUE (AnalysisId, IdentityId)
);

-- The actions inside a generated role. IsRare = used fewer times than the rare threshold.
CREATE TABLE gov.GeneratedRoleActions (
    GeneratedRoleActionId INT IDENTITY(1,1) CONSTRAINT PK_GenRoleActions PRIMARY KEY,
    GeneratedRoleId INT          NOT NULL CONSTRAINT FK_GenRoleActions_Roles REFERENCES gov.GeneratedRoles (GeneratedRoleId) ON DELETE CASCADE,
    Action          VARCHAR(200) NOT NULL,
    IsRare          BIT          NOT NULL CONSTRAINT DF_GenRoleActions_Rare DEFAULT (0),
    Note            VARCHAR(200) NULL,      -- "used once, 85 days ago"
    CONSTRAINT UQ_GenRoleActions UNIQUE (GeneratedRoleId, Action)
);

-- Rules Configuration page (one active row). Weights must add up to 100.
CREATE TABLE gov.RuleSettings (
    RuleSettingId       INT IDENTITY(1,1) CONSTRAINT PK_RuleSettings PRIMARY KEY,
    WeightUnused        INT NOT NULL CONSTRAINT CK_Rules_W1 CHECK (WeightUnused BETWEEN 0 AND 100),
    WeightHighRisk      INT NOT NULL CONSTRAINT CK_Rules_W2 CHECK (WeightHighRisk BETWEEN 0 AND 100),
    WeightScope         INT NOT NULL CONSTRAINT CK_Rules_W3 CHECK (WeightScope BETWEEN 0 AND 100),
    DefaultPeriodDays   INT NOT NULL CONSTRAINT CK_Rules_Period CHECK (DefaultPeriodDays BETWEEN 7 AND 180),
    RareThreshold       INT NOT NULL CONSTRAINT CK_Rules_Rare CHECK (RareThreshold BETWEEN 1 AND 50),
    IncludeBaselineRead BIT NOT NULL,
    IsActive            BIT NOT NULL CONSTRAINT DF_Rules_Active DEFAULT (1),
    UpdatedBy           INT NULL CONSTRAINT FK_Rules_Users REFERENCES auth.Users (UserId),
    UpdatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Rules_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Rules_Total CHECK (WeightUnused + WeightHighRisk + WeightScope = 100)
);

-- High-risk action patterns, for example "any delete action" or "anything in Key Vault".
CREATE TABLE gov.HighRiskPatterns (
    PatternId     INT IDENTITY(1,1) CONSTRAINT PK_HighRiskPatterns PRIMARY KEY,
    Pattern       VARCHAR(200) NOT NULL
                               CONSTRAINT CK_Patterns_Chars CHECK (Pattern NOT LIKE '%[^A-Za-z0-9./*-]%'),
    IsEnabled     BIT          NOT NULL CONSTRAINT DF_Patterns_Enabled DEFAULT (1),
    CONSTRAINT UQ_Patterns UNIQUE (Pattern)
);

-- Audit-ready HTML reports.
CREATE TABLE gov.Reports (
    ReportId      INT IDENTITY(1,1) CONSTRAINT PK_Reports PRIMARY KEY,
    Code          VARCHAR(20)  NOT NULL,   -- RPT-1110
    AnalysisId    INT          NOT NULL CONSTRAINT FK_Reports_Analyses REFERENCES gov.Analyses (AnalysisId),
    IdentityCount INT          NOT NULL,
    AvgScore      DECIMAL(5,2) NOT NULL,
    HighRiskCount INT          NOT NULL,
    GeneratedBy   INT          NOT NULL CONSTRAINT FK_Reports_Users REFERENCES auth.Users (UserId),
    GeneratedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_Reports_Generated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Reports_Code UNIQUE (Code)
);

-- Bell notifications for the Cloud Admin.
CREATE TABLE gov.Notifications (
    NotificationId INT IDENTITY(1,1) CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId         INT          NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES auth.Users (UserId),
    Message        VARCHAR(300) NOT NULL,
    IsRead         BIT          NOT NULL CONSTRAINT DF_Notifications_Read DEFAULT (0),
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Notifications_Created DEFAULT (SYSUTCDATETIME())
);
GO

/* =====================================================================
   EMDT - Enterprise Modernization Digital Twin
   Flow: Enterprise -> Classification (EMSI) -> Applications + Components
         -> Dependencies -> Assessment -> Simulation -> Plan -> Decision
   ===================================================================== */

CREATE TABLE emdt.Enterprises (
    EnterpriseId  INT IDENTITY(1,1) CONSTRAINT PK_Enterprises PRIMARY KEY,
    Code          AS ('ENT-' + RIGHT('000' + CAST(EnterpriseId AS VARCHAR(10)), 3)) PERSISTED,
    Name          VARCHAR(150) NOT NULL CONSTRAINT CK_Enterprises_Name CHECK (LEN(Name) >= 3),
    Industry      VARCHAR(50)  NOT NULL,
    City          VARCHAR(50)  NULL,
    ContactName   VARCHAR(50)  NOT NULL,
    ContactEmail  VARCHAR(150) NOT NULL CONSTRAINT CK_Enterprises_Email CHECK (ContactEmail LIKE '_%@_%._%'),
    ContactPhone  CHAR(10)     NULL     CONSTRAINT CK_Enterprises_Phone CHECK (ContactPhone LIKE '[6-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    RegisteredBy  INT          NULL     CONSTRAINT FK_Enterprises_Users REFERENCES auth.Users (UserId),
    RegisteredAt  DATE         NOT NULL CONSTRAINT DF_Enterprises_Reg DEFAULT (CAST(SYSUTCDATETIME() AS DATE)),
    CONSTRAINT UQ_Enterprises_Name UNIQUE (Name)
);

-- The 12 classification answers + the EMSI result (one per enterprise).
CREATE TABLE emdt.Classifications (
    ClassificationId INT IDENTITY(1,1) CONSTRAINT PK_Classifications PRIMARY KEY,
    EnterpriseId  INT           NOT NULL
                                CONSTRAINT FK_Classifications_Ent REFERENCES emdt.Enterprises (EnterpriseId),
    TotalApps     INT           NOT NULL CONSTRAINT CK_Cls_Apps CHECK (TotalApps BETWEEN 1 AND 10000),
    Dependencies  INT           NOT NULL CONSTRAINT CK_Cls_Deps CHECK (Dependencies >= 0),
    Apis          INT           NOT NULL CONSTRAINT CK_Cls_Apis CHECK (Apis >= 0),
    DatabaseCount INT           NOT NULL CONSTRAINT CK_Cls_Dbs  CHECK (DatabaseCount >= 0),
    StorageGB     INT           NOT NULL CONSTRAINT CK_Cls_Stor CHECK (StorageGB >= 0),
    Environments  INT           NOT NULL CONSTRAINT CK_Cls_Envs CHECK (Environments BETWEEN 1 AND 20),
    BusinessUnits INT           NOT NULL CONSTRAINT CK_Cls_Units CHECK (BusinessUnits >= 1),
    UserCount     INT           NOT NULL CONSTRAINT CK_Cls_Users CHECK (UserCount >= 1),
    CritLow       INT           NOT NULL,
    CritMedium    INT           NOT NULL,
    CritHigh      INT           NOT NULL,
    CritCritical  INT           NOT NULL,
    CloudType     VARCHAR(20)   NOT NULL CONSTRAINT CK_Cls_Cloud CHECK (CloudType IN ('On-Premise', 'Private Cloud', 'Public Cloud', 'Hybrid Cloud', 'Multi Cloud')),
    LinesOfCode   BIGINT        NOT NULL CONSTRAINT CK_Cls_Loc CHECK (LinesOfCode >= 0),
    EmsiScore     DECIMAL(5,2)  NOT NULL CONSTRAINT CK_Cls_Emsi CHECK (EmsiScore BETWEEN 0 AND 100),
    Category      AS (CASE WHEN EmsiScore < 25 THEN 'Foundation' WHEN EmsiScore < 45 THEN 'Growth'
                           WHEN EmsiScore < 70 THEN 'Enterprise' ELSE 'Strategic' END),
    ClassifiedBy  INT           NULL CONSTRAINT FK_Classifications_Users REFERENCES auth.Users (UserId),
    ClassifiedAt  DATE          NOT NULL CONSTRAINT DF_Classifications_At DEFAULT (CAST(SYSUTCDATETIME() AS DATE)),
    -- the 4 criticality counts must add up to total apps (same rule as the form)
    CONSTRAINT CK_Cls_CritSum CHECK (CritLow + CritMedium + CritHigh + CritCritical = TotalApps),
    CONSTRAINT UQ_Classifications_Ent UNIQUE (EnterpriseId)
);

CREATE TABLE emdt.ClassificationTechnologies (
    ClassificationId INT         NOT NULL CONSTRAINT FK_ClsTech_Cls REFERENCES emdt.Classifications (ClassificationId) ON DELETE CASCADE,
    Technology       VARCHAR(20) NOT NULL CONSTRAINT CK_ClsTech CHECK (Technology IN ('Java', 'PHP', '.NET', 'COBOL')),
    CONSTRAINT PK_ClassificationTechnologies PRIMARY KEY (ClassificationId, Technology)
);

CREATE TABLE emdt.ClassificationPolicies (
    ClassificationId INT         NOT NULL CONSTRAINT FK_ClsPol_Cls REFERENCES emdt.Classifications (ClassificationId) ON DELETE CASCADE,
    PolicyName       VARCHAR(30) NOT NULL,      -- GDPR, PCI DSS, ISO 27001, SOC 2, HIPAA ...
    CONSTRAINT PK_ClassificationPolicies PRIMARY KEY (ClassificationId, PolicyName)
);

CREATE TABLE emdt.Applications (
    ApplicationId INT IDENTITY(1,1) CONSTRAINT PK_Applications PRIMARY KEY,
    Code          AS ('APP-' + RIGHT('000' + CAST(ApplicationId AS VARCHAR(10)), 3)) PERSISTED,
    EnterpriseId  INT          NOT NULL CONSTRAINT FK_Applications_Ent REFERENCES emdt.Enterprises (EnterpriseId),
    Name          VARCHAR(100) NOT NULL CONSTRAINT CK_Apps_Name CHECK (LEN(Name) >= 3),
    Technology    VARCHAR(20)  NOT NULL CONSTRAINT CK_Apps_Tech CHECK (Technology IN ('Java', 'PHP', '.NET', 'COBOL')),
    Framework     VARCHAR(50)  NOT NULL,
    SupportStatus VARCHAR(12)  NOT NULL CONSTRAINT CK_Apps_Support CHECK (SupportStatus IN ('Supported', 'Extended', 'Unsupported')),
    AgeYears      INT          NOT NULL CONSTRAINT CK_Apps_Age CHECK (AgeYears BETWEEN 0 AND 50),
    Criticality   VARCHAR(10)  NOT NULL CONSTRAINT CK_Apps_Crit CHECK (Criticality IN ('Low', 'Medium', 'High', 'Critical')),
    HasSensitiveData BIT       NOT NULL,
    SecurityIssues INT         NOT NULL CONSTRAINT CK_Apps_Sec CHECK (SecurityIssues >= 0),
    DowntimeTolerance VARCHAR(10) NOT NULL CONSTRAINT CK_Apps_Down CHECK (DowntimeTolerance IN ('Low', 'Medium', 'High')),
    CompliancePolicies INT     NOT NULL CONSTRAINT CK_Apps_Comp CHECK (CompliancePolicies >= 0),
    Deployment    VARCHAR(20)  NOT NULL CONSTRAINT CK_Apps_Deploy CHECK (Deployment IN ('On-Premise', 'Private Cloud', 'Public Cloud', 'Hybrid Cloud')),
    DocQuality    VARCHAR(10)  NOT NULL CONSTRAINT CK_Apps_Doc CHECK (DocQuality IN ('Good', 'Average', 'Poor')),
    Complexity    VARCHAR(10)  NOT NULL CONSTRAINT CK_Apps_Cx CHECK (Complexity IN ('Low', 'Medium', 'High')),
    LinesOfCode   BIGINT       NOT NULL CONSTRAINT CK_Apps_Loc CHECK (LinesOfCode > 0),
    Status        VARCHAR(10)  NOT NULL CONSTRAINT CK_Apps_Status CHECK (Status IN ('Active', 'Legacy', 'Retired')),
    BusinessUnit  VARCHAR(100) NULL,
    OwnerTeam     VARCHAR(100) NOT NULL,
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Apps_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Apps_Name UNIQUE (EnterpriseId, Name)
);

-- Services, APIs and databases (they may belong to an application).
CREATE TABLE emdt.Components (
    ComponentId   INT IDENTITY(1,1) CONSTRAINT PK_Components PRIMARY KEY,
    Code          AS ('N-' + RIGHT('00' + CAST(ComponentId AS VARCHAR(10)), 2)) PERSISTED,
    EnterpriseId  INT          NOT NULL CONSTRAINT FK_Components_Ent REFERENCES emdt.Enterprises (EnterpriseId),
    ApplicationId INT          NULL     CONSTRAINT FK_Components_App REFERENCES emdt.Applications (ApplicationId),  -- owner app
    ComponentType VARCHAR(10)  NOT NULL CONSTRAINT CK_Components_Type CHECK (ComponentType IN ('Service', 'API', 'Database')),
    Name          VARCHAR(100) NOT NULL,
    CONSTRAINT UQ_Components_Name UNIQUE (EnterpriseId, Name)
);

-- A link "source needs target". Source and target can each be an application OR a component.
CREATE TABLE emdt.Dependencies (
    DependencyId  INT IDENTITY(1,1) CONSTRAINT PK_Dependencies PRIMARY KEY,
    Code          AS ('DEP-' + RIGHT('000' + CAST(DependencyId AS VARCHAR(10)), 3)) PERSISTED,
    EnterpriseId  INT          NOT NULL CONSTRAINT FK_Deps_Ent REFERENCES emdt.Enterprises (EnterpriseId),
    SourceApplicationId INT    NULL CONSTRAINT FK_Deps_SrcApp  REFERENCES emdt.Applications (ApplicationId),
    SourceComponentId   INT    NULL CONSTRAINT FK_Deps_SrcComp REFERENCES emdt.Components (ComponentId),
    TargetApplicationId INT    NULL CONSTRAINT FK_Deps_TgtApp  REFERENCES emdt.Applications (ApplicationId),
    TargetComponentId   INT    NULL CONSTRAINT FK_Deps_TgtComp REFERENCES emdt.Components (ComponentId),
    Criticality   VARCHAR(10)  NOT NULL CONSTRAINT CK_Deps_Crit CHECK (Criticality IN ('Low', 'Medium', 'High', 'Critical')),
    Description   VARCHAR(200) NULL,
    CreatedBy     INT          NULL CONSTRAINT FK_Deps_Users REFERENCES auth.Users (UserId),
    -- exactly one source and exactly one target
    CONSTRAINT CK_Deps_OneSource CHECK ((SourceApplicationId IS NOT NULL AND SourceComponentId IS NULL) OR
                                        (SourceApplicationId IS NULL AND SourceComponentId IS NOT NULL)),
    CONSTRAINT CK_Deps_OneTarget CHECK ((TargetApplicationId IS NOT NULL AND TargetComponentId IS NULL) OR
                                        (TargetApplicationId IS NULL AND TargetComponentId IS NOT NULL)),
    -- not linked to itself
    CONSTRAINT CK_Deps_NotSelf CHECK (ISNULL(SourceApplicationId, -1) <> ISNULL(TargetApplicationId, -2)
                                  AND ISNULL(SourceComponentId, -1)   <> ISNULL(TargetComponentId, -2))
);
CREATE UNIQUE INDEX UX_Deps_Pair ON emdt.Dependencies (SourceApplicationId, SourceComponentId, TargetApplicationId, TargetComponentId);

-- Architect runs an assessment of one application.
CREATE TABLE emdt.Assessments (
    AssessmentId  INT IDENTITY(1,1) CONSTRAINT PK_Assessments PRIMARY KEY,
    Code          AS ('ASM-' + RIGHT('000' + CAST(AssessmentId AS VARCHAR(10)), 3)) PERSISTED,
    ApplicationId INT          NOT NULL CONSTRAINT FK_Assessments_App REFERENCES emdt.Applications (ApplicationId),
    TechnicalDebt INT          NOT NULL CONSTRAINT CK_Asm_TD CHECK (TechnicalDebt BETWEEN 0 AND 100),
    GovernanceRisk INT         NOT NULL CONSTRAINT CK_Asm_GR CHECK (GovernanceRisk BETWEEN 0 AND 100),
    DependencyImpact INT       NOT NULL CONSTRAINT CK_Asm_DI CHECK (DependencyImpact BETWEEN 0 AND 100),
    AssessedBy    INT          NOT NULL CONSTRAINT FK_Assessments_Users REFERENCES auth.Users (UserId),
    AssessedAt    DATE         NOT NULL CONSTRAINT DF_Assessments_At DEFAULT (CAST(SYSUTCDATETIME() AS DATE))
);
CREATE INDEX IX_Assessments_App ON emdt.Assessments (ApplicationId, AssessedAt DESC);

-- The reason for each point (shown on the Assessments page).
CREATE TABLE emdt.AssessmentFactors (
    AssessmentFactorId INT IDENTITY(1,1) CONSTRAINT PK_AssessmentFactors PRIMARY KEY,
    AssessmentId  INT          NOT NULL CONSTRAINT FK_AsmFactors_Asm REFERENCES emdt.Assessments (AssessmentId) ON DELETE CASCADE,
    ScoreType     VARCHAR(20)  NOT NULL CONSTRAINT CK_AsmFactors_Type CHECK (ScoreType IN ('TechnicalDebt', 'GovernanceRisk')),
    FactorName    VARCHAR(50)  NOT NULL,      -- Application age
    FactorValue   VARCHAR(100) NOT NULL,      -- 14 years
    Points        INT          NOT NULL
);

-- What-if: one application + one strategy.
CREATE TABLE emdt.Simulations (
    SimulationId  INT IDENTITY(1,1) CONSTRAINT PK_Simulations PRIMARY KEY,
    Code          AS ('SIM-' + RIGHT('000' + CAST(SimulationId AS VARCHAR(10)), 3)) PERSISTED,
    ApplicationId INT           NOT NULL CONSTRAINT FK_Simulations_App REFERENCES emdt.Applications (ApplicationId),
    AssessmentId  INT           NOT NULL CONSTRAINT FK_Simulations_Asm REFERENCES emdt.Assessments (AssessmentId),
    Strategy      VARCHAR(12)   NOT NULL CONSTRAINT CK_Sim_Strategy CHECK (Strategy IN ('Rehost', 'Replatform', 'Refactor', 'Rebuild')),
    StrategyRisk  INT           NOT NULL,
    EmsiScore     DECIMAL(5,2)  NULL,          -- enterprise EMSI at the time of the simulation
    ModernizationRisk INT       NOT NULL CONSTRAINT CK_Sim_MR CHECK (ModernizationRisk BETWEEN 0 AND 100),
    RiskLevel     AS (CASE WHEN ModernizationRisk >= 70 THEN 'High' WHEN ModernizationRisk >= 40 THEN 'Medium' ELSE 'Low' END),
    EffortWeeks   INT           NOT NULL CONSTRAINT CK_Sim_Effort CHECK (EffortWeeks > 0),
    TeamSize      INT           NOT NULL CONSTRAINT CK_Sim_Team CHECK (TeamSize BETWEEN 1 AND 50),
    CostLakh      DECIMAL(10,2) NOT NULL CONSTRAINT CK_Sim_Cost CHECK (CostLakh >= 0),   -- in Rs lakh
    Recommendation VARCHAR(500) NULL,
    CreatedBy     INT           NOT NULL CONSTRAINT FK_Simulations_Users REFERENCES auth.Users (UserId),
    CreatedAt     DATE          NOT NULL CONSTRAINT DF_Simulations_At DEFAULT (CAST(SYSUTCDATETIME() AS DATE))
    -- "Discard / Modify" deletes the row (and its impacts), so there is no status column.
);

-- Components / apps affected by a simulation, in migration order.
CREATE TABLE emdt.SimulationImpacts (
    SimulationImpactId INT IDENTITY(1,1) CONSTRAINT PK_SimulationImpacts PRIMARY KEY,
    SimulationId  INT          NOT NULL CONSTRAINT FK_SimImpacts_Sim REFERENCES emdt.Simulations (SimulationId) ON DELETE CASCADE,
    SequenceNo    INT          NOT NULL,
    AffectedName  VARCHAR(100) NOT NULL,
    AffectedType  VARCHAR(12)  NOT NULL CONSTRAINT CK_SimImpacts_Type CHECK (AffectedType IN ('Application', 'Service', 'API', 'Database')),
    Criticality   VARCHAR(10)  NOT NULL,
    OwnerName     VARCHAR(100) NULL
);

-- A plan is made from exactly one simulation. Draft -> Submitted -> Approved / Rejected.
CREATE TABLE emdt.Plans (
    PlanId        INT IDENTITY(1,1) CONSTRAINT PK_Plans PRIMARY KEY,
    Code          AS ('PLAN-' + RIGHT('000' + CAST(PlanId AS VARCHAR(10)), 3)) PERSISTED,
    SimulationId  INT          NOT NULL CONSTRAINT FK_Plans_Sim REFERENCES emdt.Simulations (SimulationId),
    PlanName      VARCHAR(150) NOT NULL CONSTRAINT CK_Plans_Name CHECK (LEN(PlanName) >= 3),
    Status        VARCHAR(10)  NOT NULL CONSTRAINT DF_Plans_Status DEFAULT ('Draft')
                               CONSTRAINT CK_Plans_Status CHECK (Status IN ('Draft', 'Submitted', 'Approved', 'Rejected')),
    Notes         VARCHAR(1000) NULL,
    CreatedBy     INT          NOT NULL CONSTRAINT FK_Plans_CreatedBy REFERENCES auth.Users (UserId),
    CreatedAt     DATE         NOT NULL CONSTRAINT DF_Plans_Created DEFAULT (CAST(SYSUTCDATETIME() AS DATE)),
    SubmittedAt   DATE         NULL,
    DecidedBy     INT          NULL CONSTRAINT FK_Plans_DecidedBy REFERENCES auth.Users (UserId),   -- Executive
    DecidedAt     DATE         NULL,
    ExecutiveComment VARCHAR(500) NULL,
    -- a rejected plan must have a reason; a decided plan must say who decided
    CONSTRAINT CK_Plans_RejectReason CHECK (Status <> 'Rejected' OR LEN(ISNULL(ExecutiveComment, '')) > 0),
    CONSTRAINT CK_Plans_Decided CHECK (Status NOT IN ('Approved', 'Rejected') OR DecidedBy IS NOT NULL),
    CONSTRAINT CK_Plans_Submitted CHECK (Status = 'Draft' OR SubmittedAt IS NOT NULL),
    CONSTRAINT UQ_Plans_Sim UNIQUE (SimulationId),
    CONSTRAINT UQ_Plans_Name UNIQUE (PlanName)
);

CREATE TABLE emdt.PlanPhases (
    PlanPhaseId   INT IDENTITY(1,1) CONSTRAINT PK_PlanPhases PRIMARY KEY,
    PlanId        INT          NOT NULL CONSTRAINT FK_PlanPhases_Plans REFERENCES emdt.Plans (PlanId) ON DELETE CASCADE,
    PhaseNo       INT          NOT NULL CONSTRAINT CK_PlanPhases_No CHECK (PhaseNo BETWEEN 1 AND 10),
    PhaseName     VARCHAR(100) NOT NULL,
    Weeks         INT          NOT NULL CONSTRAINT CK_PlanPhases_Weeks CHECK (Weeks >= 0),
    CONSTRAINT UQ_PlanPhases UNIQUE (PlanId, PhaseNo)
);
GO

/* =====================================================================
   Helpful views (used by the dashboards)
   ===================================================================== */

-- Login: email -> role -> home page
CREATE OR ALTER VIEW auth.vw_UserLogin AS
SELECT u.UserId, u.Code, u.FullName, u.Email, u.PasswordHash, u.Status,
       r.RoleCode, r.RoleName, r.Product, r.HomePage
FROM auth.Users u
JOIN auth.Roles r ON r.RoleId = u.RoleId;
GO

-- Latest assessment of every application
CREATE OR ALTER VIEW emdt.vw_LatestAssessment AS
SELECT a.*
FROM emdt.Assessments a
WHERE a.AssessmentId = (SELECT TOP 1 x.AssessmentId FROM emdt.Assessments x
                        WHERE x.ApplicationId = a.ApplicationId
                        ORDER BY x.AssessedAt DESC, x.AssessmentId DESC);
GO

-- Plans with application, enterprise and numbers (Executive Approvals page)
CREATE OR ALTER VIEW emdt.vw_PlanSummary AS
SELECT p.PlanId, p.Code AS PlanCode, p.PlanName, p.Status, p.CreatedAt, p.SubmittedAt, p.DecidedAt,
       s.Code AS SimulationCode, s.Strategy, s.ModernizationRisk, s.RiskLevel, s.EffortWeeks, s.TeamSize, s.CostLakh,
       a.Code AS ApplicationCode, a.Name AS ApplicationName,
       e.Code AS EnterpriseCode, e.Name AS EnterpriseName,
       cu.FullName AS CreatedByName, du.FullName AS DecidedByName, p.ExecutiveComment
FROM emdt.Plans p
JOIN emdt.Simulations  s ON s.SimulationId  = p.SimulationId
JOIN emdt.Applications a ON a.ApplicationId = s.ApplicationId
JOIN emdt.Enterprises  e ON e.EnterpriseId  = a.EnterpriseId
JOIN auth.Users cu ON cu.UserId = p.CreatedBy
LEFT JOIN auth.Users du ON du.UserId = p.DecidedBy;
GO

-- Identity list of an analysis (Identities page)
CREATE OR ALTER VIEW gov.vw_IdentityRisk AS
SELECT s.AnalysisId, an.Code AS AnalysisCode, i.IdentityId, i.UserName, i.DisplayName, i.IdentityType,
       i.AssignedRole, i.Scope, i.ScopeName, s.GrantedCount, s.UsedCount, s.RiskScore, s.RiskLevel
FROM gov.IdentityScores s
JOIN gov.Identities i ON i.IdentityId = s.IdentityId
JOIN gov.Analyses  an ON an.AnalysisId = s.AnalysisId;
GO

PRINT 'SecureShiftDB schema created: 3 schemas, 28 tables, 4 views.';
GO
