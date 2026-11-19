/* =====================================================================
   SecureShift - 03_sample_queries.sql
   Example queries the backend (Phase 3, ASP.NET API) will need.
   Run after 01 and 02. Each query matches a screen in the prototype.
   ===================================================================== */
USE SecureShiftDB;
GO

/* ---------- Login: find the user by email, then check the password hash ---------- */
DECLARE @Email VARCHAR(150) = 'amanjeet.sharma@secureshift.io';
DECLARE @Password VARCHAR(100) = 'Architect@2026';
SELECT UserId, Code, FullName, RoleName, Product, HomePage          -- HomePage = role-based redirect
FROM auth.vw_UserLogin
WHERE Email = @Email
  AND Status = 'Active'
  AND PasswordHash = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @Password), 2);
-- (In Phase 3 use ASP.NET Core Identity PasswordHasher with salt instead of plain SHA-256.)

/* ---------- Super Admin > Users (search + role filter) ---------- */
SELECT u.Code, u.FullName, u.Email, u.Phone, r.RoleName, u.Status, u.CreatedAt
FROM auth.Users u JOIN auth.Roles r ON r.RoleId = u.RoleId
WHERE r.Product = 'Modernization'
  AND (u.FullName LIKE '%' + 'sh' + '%' OR u.Email LIKE '%' + 'sh' + '%')
ORDER BY u.UserId;

/* ---------- Super Admin > Add User: insert the user (login details are shared manually, no email) ---------- */
-- BEGIN TRANSACTION;
-- INSERT INTO auth.Users (FullName, Email, Phone, PasswordHash, RoleId, CreatedBy)
-- VALUES ('Rahul Verma', 'rahul.verma@company.com', '9876500011',
--         CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Temp@4821x'), 2), 4, 1);   -- temporary password shown to the Super Admin
-- INSERT INTO auth.AuditLogs (UserId, Action, Target, Detail) VALUES (1, 'User Created', 'Rahul Verma', 'Role: Architect');
-- COMMIT;

/* ---------- Governance dashboard: latest analysis of a company ---------- */
SELECT TOP 1 a.Code, c.CompanyName, a.PeriodFrom, a.PeriodTo, a.PeriodDays, a.IdentityCount, a.AvgScore, a.HighRiskCount
FROM gov.Analyses a JOIN gov.Companies c ON c.CompanyId = a.CompanyId
WHERE c.CompanyName = 'Northwind Traders'
ORDER BY a.RunAt DESC;

/* ---------- Governance: top 5 over-privileged identities ---------- */
SELECT TOP 5 DisplayName, UserName, IdentityType, AssignedRole, RiskScore, RiskLevel
FROM gov.vw_IdentityRisk
WHERE AnalysisCode = 'AN-2026-1110'
ORDER BY RiskScore DESC;

/* ---------- Governance: High-Risk Findings by category ---------- */
SELECT f.Category, COUNT(*) AS OpenFindings
FROM gov.Findings f
WHERE f.Status = 'Open'
GROUP BY f.Category;

/* ---------- Admin: enterprises with EMSI and category ---------- */
SELECT e.Code, e.Name, e.Industry, c.EmsiScore, c.Category,
       (SELECT COUNT(*) FROM emdt.Applications a WHERE a.EnterpriseId = e.EnterpriseId) AS Applications
FROM emdt.Enterprises e
LEFT JOIN emdt.Classifications c ON c.EnterpriseId = e.EnterpriseId;

/* ---------- Admin: dependencies of one application (outgoing links) ---------- */
SELECT d.Code, a.Name AS SourceApp,
       COALESCE(ta.Name, tc.Name) AS Target, COALESCE(tc.ComponentType, 'Application') AS TargetType, d.Criticality
FROM emdt.Dependencies d
JOIN emdt.Applications a  ON a.ApplicationId  = d.SourceApplicationId
LEFT JOIN emdt.Applications ta ON ta.ApplicationId = d.TargetApplicationId
LEFT JOIN emdt.Components   tc ON tc.ComponentId   = d.TargetComponentId
WHERE a.Name = 'Trade Finance Portal';

/* ---------- Architect: applications not assessed yet ---------- */
SELECT a.Code, a.Name, a.Criticality
FROM emdt.Applications a
WHERE NOT EXISTS (SELECT 1 FROM emdt.Assessments s WHERE s.ApplicationId = a.ApplicationId);

/* ---------- Executive: plans waiting for a decision ---------- */
SELECT PlanCode, PlanName, ApplicationName, EnterpriseName, Strategy, EffortWeeks, CostLakh, CreatedByName
FROM emdt.vw_PlanSummary
WHERE Status = 'Submitted'
ORDER BY SubmittedAt;

/* ---------- Executive: approve a plan ---------- */
-- UPDATE emdt.Plans SET Status = 'Approved', DecidedBy = 14, DecidedAt = CAST(SYSUTCDATETIME() AS DATE),
--        ExecutiveComment = 'Approved for Q1.'
-- WHERE PlanId = 1 AND Status = 'Submitted';
GO
