/* =====================================================================
   SecureShift - 02_seed_data.sql
   Sample data for SecureShiftDB. Run AFTER 01_create_schema.sql.
   Generated from the Sprint 1 prototype hardcoded data (the js/data folder),
   so the database shows exactly the same numbers as the screens.

   Login accounts (passwords are stored as SHA-256 hashes):
     Cloud Admin  aarav.mehta@secureshift.io          Cloud@2026
     Super Admin  arya.mujumdar@secureshift.io        Super@2026
     Admin        sachin.kumar@secureshift.io         Admin@2026
     Architect    amanjeet.sharma@secureshift.io      Architect@2026
     Executive    shivanand.kumatekar@secureshift.io  Exec@2026
   All other users get the temporary password Welcome@2026 (must change).
   No emails are sent: the Super Admin gives login details to users manually.
   ===================================================================== */
USE SecureShiftDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;      -- if any insert fails, everything is rolled back
BEGIN TRANSACTION;

-- auth.Roles: 5 rows
SET IDENTITY_INSERT auth.Roles ON;
INSERT INTO auth.Roles (RoleId, RoleCode, RoleName, Product, HomePage) VALUES
  (1, 'cloudadmin', 'Cloud Admin', 'Governance', 'governance/dashboard.html'),
  (2, 'superadmin', 'Super Admin', 'Modernization', 'modernization/superadmin-dashboard.html'),
  (3, 'admin', 'Admin', 'Modernization', 'modernization/admin-dashboard.html'),
  (4, 'architect', 'Architect', 'Modernization', 'modernization/architect-dashboard.html'),
  (5, 'executive', 'Executive', 'Modernization', 'modernization/executive-dashboard.html');
SET IDENTITY_INSERT auth.Roles OFF;

-- auth.Users: 25 rows
SET IDENTITY_INSERT auth.Users ON;
INSERT INTO auth.Users (UserId, FullName, Email, Phone, PasswordHash, RoleId, Department, Status, MustChangePassword, CreatedAt, CreatedBy) VALUES
  (1, 'Arya Mujumdar', 'arya.mujumdar@secureshift.io', '9876543210', '04B4B063C2038952ABEC8E274F99A6B9A964642D3CF5E98707AAE82148F29F05', 2, 'Platform Administration', 'Active', 0, '2026-01-10', NULL),
  (2, 'Rohan Kapoor', 'rohan.kapoor@company.com', '9876543211', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 2, 'Platform Administration', 'Active', 1, '2026-02-11', 1),
  (3, 'Tejas Pathak', 'tejas.pathak@company.com', '9876543212', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Active', 1, '2026-03-12', 1),
  (4, 'Aditya Ghate', 'aditya.ghate@company.com', '9876543213', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 3, 'Modernization Office', 'Active', 1, '2026-04-13', 1),
  (5, 'Sonal Shende', 'sonal.shende@company.com', '9876543214', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Active', 1, '2026-05-14', 1),
  (6, 'Sakshi Patil', 'sakshi.patil@company.com', '9876543215', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 4, 'Modernization Office', 'Inactive', 1, '2026-06-15', 1),
  (7, 'Vivek Randive', 'vivek.randive@company.com', '9876543216', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 3, 'Modernization Office', 'Active', 1, '2026-07-16', 1),
  (8, 'Aditi Jain', 'aditi.jain@company.com', '9876543217', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Active', 1, '2026-08-17', 1),
  (9, 'Rohit Kale', 'rohit.kale@company.com', '9876543218', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 4, 'Modernization Office', 'Inactive', 1, '2026-09-18', 1),
  (10, 'Sneha Gupta', 'sneha.gupta@company.com', '9876543219', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 3, 'Modernization Office', 'Active', 1, '2026-01-10', 1),
  (11, 'Tanvi Mahore', 'tanvi.mahore@company.com', '9876543220', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Active', 1, '2026-02-11', 1),
  (12, 'Sachin Kumar', 'sachin.kumar@secureshift.io', '9876543221', 'A36AEF5A11C4073FBE60314FC9DF530A9D5F986533594D1F5190742FF9E0E408', 3, 'Modernization Office', 'Active', 0, '2026-03-12', 1),
  (13, 'Amanjeet Sharma', 'amanjeet.sharma@secureshift.io', '9876543222', 'AA08CFFC13B7855277A11861790966E5A8BDD351522CD759D6E5D62C7B797DDE', 4, 'Modernization Office', 'Active', 0, '2026-04-13', 1),
  (14, 'Shivanand Kumatekar', 'shivanand.kumatekar@secureshift.io', '9876543223', '57F1E7675C7FBB9823740AE3816FD4E715CEA216261EFCD5D57785AD10027CB3', 5, 'Modernization Office', 'Active', 0, '2026-05-14', 1),
  (15, 'Mohit Choudhary', 'mohit.choudhary@company.com', '9876543224', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 4, 'Modernization Office', 'Active', 1, '2026-06-15', 1),
  (16, 'Shrinivas Naik', 'shrinivas.naik@company.com', '9876543225', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 4, 'Modernization Office', 'Active', 1, '2026-07-16', 1),
  (17, 'Pooja Kulkarni', 'pooja.kulkarni@company.com', '9876543226', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 3, 'Modernization Office', 'Active', 1, '2026-08-17', 1),
  (18, 'Nikhil Bhosale', 'nikhil.bhosale@company.com', '9876543227', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 4, 'Modernization Office', 'Inactive', 1, '2026-09-18', 1),
  (19, 'Rutuja More', 'rutuja.more@company.com', '9876543228', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Active', 1, '2026-01-10', 1),
  (20, 'Omkar Jadhav', 'omkar.jadhav@company.com', '9876543229', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 3, 'Modernization Office', 'Active', 1, '2026-02-11', 1),
  (21, 'Isha Deshpande', 'isha.deshpande@company.com', '9876543230', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 4, 'Modernization Office', 'Active', 1, '2026-03-12', 1),
  (22, 'Kunal Shinde', 'kunal.shinde@company.com', '9876543231', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Active', 1, '2026-04-13', 1),
  (23, 'Meera Nair', 'meera.nair@company.com', '9876543232', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 3, 'Modernization Office', 'Active', 1, '2026-05-14', 1),
  (24, 'Divya Rao', 'divya.rao@company.com', '9876543233', '3E62B2186F7F32B65F35894C06C3E823BC4E2940888FC04A56A5462BE416F3D2', 5, 'Modernization Office', 'Inactive', 1, '2026-06-15', 1),
  (25, 'Aarav Mehta', 'aarav.mehta@secureshift.io', '9811122233', 'AFBABBDC45B1EB528A3099C4EF03099146728C61CD13FF4DA3BF1139D3F9ED91', 1, 'Cloud Security', 'Active', 0, '2026-01-10', 1);
SET IDENTITY_INSERT auth.Users OFF;

-- auth.AuditLogs: 8 rows
INSERT INTO auth.AuditLogs (UserId, Action, Target, Detail, CreatedAt) VALUES
  (1, 'Password Reset', 'Aditi Jain', 'Temporary password given manually', '2026-11-08T10:45:00'),
  (12, 'Dependency Added', 'Payment Service → Customer API', 'API', '2026-11-09T14:19:00'),
  (14, 'Plan Approved', 'PLAN-004', 'Online Store', '2026-11-09T17:33:00'),
  (13, 'Simulation Run', 'Trade Finance Portal', 'Refactor', '2026-11-10T11:02:00'),
  (12, 'Enterprise Classified', 'Northwind Bank', 'EMSI calculated', '2026-11-10T16:12:00'),
  (1, 'Account Deactivated', 'Rohit Kale', 'Left project', '2026-11-10T18:40:00'),
  (1, 'Role Changed', 'Sonal Shende', 'Admin → Executive', '2026-11-11T09:51:00'),
  (1, 'User Created', 'Rutuja More', 'Role: Executive', '2026-11-11T10:24:00');

-- gov.Companies: 10 rows
SET IDENTITY_INSERT gov.Companies ON;
INSERT INTO gov.Companies (CompanyId, CompanyName, TenantId, Subscriptions, CreatedBy, CreatedAt) VALUES
  (1, 'Northwind Traders', 'd4d09c07-07f6-4c71-4a6f-beaa7d6d22bc', 2, 25, '2026-09-01'),
  (2, 'Contoso Retail', '317ae58c-152a-72e8-4be9-2939ff262354', 3, 25, '2026-09-01'),
  (3, 'Fabrikam Logistics', '47dd0ceb-69f6-5926-cc11-ee925b44b4a3', 4, 25, '2026-09-01'),
  (4, 'Tailspin Airlines', '4323d38c-658e-905f-d9c6-1a15a074cda8', 1, 25, '2026-09-01'),
  (5, 'Adventure Works', '9187a08b-a365-c0f2-4ec0-f2e961e9bd20', 2, 25, '2026-09-01'),
  (6, 'Litware Health', '1f835e82-65dc-0c80-6fb5-589e94d4eba1', 3, 25, '2026-09-01'),
  (7, 'Proseware Finance', 'c43d0ea2-b3bc-a6e0-acaa-91bf53da60ce', 4, 25, '2026-09-01'),
  (8, 'Wingtip Media', 'd76dbb03-d277-465a-ae15-ec7b35333352', 1, 25, '2026-09-01'),
  (9, 'Fourth Coffee', '55cc676b-402a-4c8e-86b0-31a0dd5a88c9', 2, 25, '2026-09-01'),
  (10, 'Woodgrove Bank', 'c290c685-387a-f91e-282b-1ea3e1629751', 3, 25, '2026-09-01');
SET IDENTITY_INSERT gov.Companies OFF;

-- gov.Analyses: 4 rows
SET IDENTITY_INSERT gov.Analyses ON;
INSERT INTO gov.Analyses (AnalysisId, Code, CompanyId, PeriodFrom, PeriodTo, Status, IdentityCount, AvgScore, HighRiskCount, RunBy, RunAt) VALUES
  (1, 'AN-2026-1110', 1, '2026-08-12', '2026-11-09', 'Complete', 128, 54, 37, 25, '2026-11-10T16:12:00'),
  (2, 'AN-2026-1103', 1, '2026-08-05', '2026-11-02', 'Complete', 126, 57, 41, 25, '2026-11-03T15:40:00'),
  (3, 'AN-2026-1027', 2, '2026-09-27', '2026-10-26', 'Complete', 74, 49, 21, 25, '2026-10-27T11:05:00'),
  (4, 'AN-2026-1020', 3, '2026-07-22', '2026-10-19', 'Complete', 91, 62, 30, 25, '2026-10-20T10:00:00');
SET IDENTITY_INSERT gov.Analyses OFF;

-- gov.Identities: 128 rows
SET IDENTITY_INSERT gov.Identities ON;
INSERT INTO gov.Identities (IdentityId, CompanyId, UserName, DisplayName, IdentityType, AssignedRole, Scope, ScopeName) VALUES
  (1, 1, 'svc-webapp-deploy', 'Web App Deploy Pipeline', 'Service Principal', 'Contributor', 'Subscription', NULL),
  (2, 1, 'rahul.verma', 'Rahul Verma', 'User', 'Owner', 'Subscription', NULL),
  (3, 1, 'priya.sharma', 'Priya Sharma', 'User', 'Contributor', 'Resource Group', 'rg-sales'),
  (4, 1, 'svc-data-pipeline', 'Data Pipeline Service', 'Service Principal', 'VM Contributor', 'Resource Group', 'rg-data'),
  (5, 1, 'mi-orders-api', 'Orders API (Managed)', 'Managed Identity', 'Reader', 'Resource Group', 'rg-orders'),
  (6, 1, 'svc-backup', 'Backup Service', 'Service Principal', 'Backup Contributor', 'Resource Group', 'rg-backup'),
  (7, 1, 'neha.kulkarni', 'Neha Kulkarni', 'User', 'Network Contributor', 'Resource Group', 'rg-network'),
  (8, 1, 'mi-reports-func', 'Reports Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-reports'),
  (9, 1, 'amit.joshi', 'Amit Joshi', 'User', 'Contributor', 'Resource Group', 'rg-hr'),
  (10, 1, 'svc-aks-ops', 'AKS Operations Service', 'Service Principal', 'AKS Contributor', 'Resource Group', 'rg-aks'),
  (11, 1, 'kavya.iyer', 'Kavya Iyer', 'User', 'Reader', 'Subscription', NULL),
  (12, 1, 'mi-billing-job', 'Billing Job (Managed)', 'Managed Identity', 'Storage Blob Contributor', 'Resource Group', 'rg-billing'),
  (13, 1, 'svc-github-ci', 'GitHub CI Pipeline', 'Service Principal', 'Owner', 'Resource Group', 'rg-webapp-prod'),
  (14, 1, 'rohan.desai', 'Rohan Desai', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-data'),
  (15, 1, 'mi-notify-func', 'Notify Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-notify'),
  (16, 1, 'sneha.patil', 'Sneha Patil', 'User', 'Key Vault Officer', 'Resource Group', 'rg-security'),
  (17, 1, 'svc-audit-016', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (18, 1, 'mi-crm-017', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (19, 1, 'harish.gupta', 'Harish Gupta', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (20, 1, 'isha.bhosale', 'Isha Bhosale', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (21, 1, 'svc-hr-020', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (22, 1, 'mi-logs-021', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (23, 1, 'lata.shinde', 'Lata Shinde', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (24, 1, 'manoj.chavan', 'Manoj Chavan', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (25, 1, 'svc-billing-024', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (26, 1, 'mi-catalog-025', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (27, 1, 'ankit.deshmukh', 'Ankit Deshmukh', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (28, 1, 'pooja.mehta', 'Pooja Mehta', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (29, 1, 'svc-audit-028', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (30, 1, 'mi-crm-029', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (31, 1, 'deepak.kulkarni', 'Deepak Kulkarni', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (32, 1, 'meera.nair', 'Meera Nair', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (33, 1, 'svc-hr-032', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (34, 1, 'mi-logs-033', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (35, 1, 'rajesh.bhosale', 'Rajesh Bhosale', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (36, 1, 'shruti.pawar', 'Shruti Pawar', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (37, 1, 'svc-billing-036', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (38, 1, 'mi-catalog-037', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (39, 1, 'sagar.chavan', 'Sagar Chavan', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (40, 1, 'swati.joshi', 'Swati Joshi', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (41, 1, 'svc-audit-040', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (42, 1, 'mi-crm-041', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (43, 1, 'chetan.mehta', 'Chetan Mehta', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (44, 1, 'divya.patil', 'Divya Patil', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (45, 1, 'svc-hr-044', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (46, 1, 'mi-logs-045', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (47, 1, 'jayesh.nair', 'Jayesh Nair', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (48, 1, 'kiran.reddy', 'Kiran Reddy', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (49, 1, 'svc-billing-048', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (50, 1, 'mi-catalog-049', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (51, 1, 'nikhil.pawar', 'Nikhil Pawar', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (52, 1, 'omkar.jadhav', 'Omkar Jadhav', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (53, 1, 'svc-audit-052', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (54, 1, 'mi-crm-053', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (55, 1, 'vikas.joshi', 'Vikas Joshi', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (56, 1, 'sonal.iyer', 'Sonal Iyer', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (57, 1, 'svc-hr-056', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (58, 1, 'mi-logs-057', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (59, 1, 'tushar.patil', 'Tushar Patil', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (60, 1, 'nisha.rao', 'Nisha Rao', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (61, 1, 'svc-billing-060', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (62, 1, 'mi-catalog-061', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (63, 1, 'gaurav.reddy', 'Gaurav Reddy', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (64, 1, 'komal.gupta', 'Komal Gupta', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (65, 1, 'svc-audit-064', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (66, 1, 'mi-crm-065', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (67, 1, 'aditya.jadhav', 'Aditya Jadhav', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (68, 1, 'bhavna.shinde', 'Bhavna Shinde', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (69, 1, 'svc-hr-068', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (70, 1, 'mi-logs-069', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (71, 1, 'harish.iyer', 'Harish Iyer', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (72, 1, 'isha.deshmukh', 'Isha Deshmukh', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (73, 1, 'svc-billing-072', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (74, 1, 'mi-catalog-073', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (75, 1, 'lata.rao', 'Lata Rao', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (76, 1, 'manoj.kulkarni', 'Manoj Kulkarni', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (77, 1, 'svc-audit-076', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (78, 1, 'mi-crm-077', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (79, 1, 'ankit.gupta', 'Ankit Gupta', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (80, 1, 'pooja.bhosale', 'Pooja Bhosale', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (81, 1, 'svc-hr-080', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (82, 1, 'mi-logs-081', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (83, 1, 'deepak.shinde', 'Deepak Shinde', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (84, 1, 'meera.chavan', 'Meera Chavan', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (85, 1, 'svc-billing-084', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (86, 1, 'mi-catalog-085', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (87, 1, 'rajesh.deshmukh', 'Rajesh Deshmukh', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (88, 1, 'shruti.mehta', 'Shruti Mehta', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (89, 1, 'svc-audit-088', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (90, 1, 'mi-crm-089', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (91, 1, 'sagar.kulkarni', 'Sagar Kulkarni', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (92, 1, 'swati.nair', 'Swati Nair', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (93, 1, 'svc-hr-092', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (94, 1, 'mi-logs-093', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (95, 1, 'chetan.bhosale', 'Chetan Bhosale', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (96, 1, 'divya.pawar', 'Divya Pawar', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (97, 1, 'svc-billing-096', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (98, 1, 'mi-catalog-097', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (99, 1, 'jayesh.chavan', 'Jayesh Chavan', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (100, 1, 'kiran.joshi', 'Kiran Joshi', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (101, 1, 'svc-audit-100', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (102, 1, 'mi-crm-101', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (103, 1, 'nikhil.mehta', 'Nikhil Mehta', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (104, 1, 'omkar.patil', 'Omkar Patil', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (105, 1, 'svc-hr-104', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (106, 1, 'mi-logs-105', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (107, 1, 'vikas.nair', 'Vikas Nair', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (108, 1, 'sonal.reddy', 'Sonal Reddy', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (109, 1, 'svc-billing-108', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (110, 1, 'mi-catalog-109', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (111, 1, 'tushar.pawar', 'Tushar Pawar', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (112, 1, 'nisha.jadhav', 'Nisha Jadhav', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (113, 1, 'svc-audit-112', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (114, 1, 'mi-crm-113', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (115, 1, 'gaurav.joshi', 'Gaurav Joshi', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (116, 1, 'komal.iyer', 'Komal Iyer', 'User', 'Contributor', 'Resource Group', 'rg-payments'),
  (117, 1, 'svc-hr-116', 'Hr Service', 'Service Principal', 'Website Contributor', 'Resource Group', 'rg-hr'),
  (118, 1, 'mi-logs-117', 'Logs Function (Managed)', 'Managed Identity', 'Monitoring Reader', 'Resource Group', 'rg-logs'),
  (119, 1, 'aditya.patil', 'Aditya Patil', 'User', 'SQL DB Contributor', 'Resource Group', 'rg-ml'),
  (120, 1, 'bhavna.rao', 'Bhavna Rao', 'User', 'Key Vault Reader', 'Resource Group', 'rg-etl'),
  (121, 1, 'svc-billing-120', 'Billing Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-billing'),
  (122, 1, 'mi-catalog-121', 'Catalog Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-catalog'),
  (123, 1, 'harish.reddy', 'Harish Reddy', 'User', 'Website Contributor', 'Resource Group', 'rg-search'),
  (124, 1, 'isha.gupta', 'Isha Gupta', 'User', 'Monitoring Reader', 'Resource Group', 'rg-media'),
  (125, 1, 'svc-audit-124', 'Audit Service', 'Service Principal', 'Storage Blob Reader', 'Resource Group', 'rg-audit'),
  (126, 1, 'mi-crm-125', 'Crm Function (Managed)', 'Managed Identity', 'Contributor', 'Resource Group', 'rg-crm'),
  (127, 1, 'lata.jadhav', 'Lata Jadhav', 'User', 'Reader', 'Resource Group', 'rg-inventory'),
  (128, 1, 'manoj.shinde', 'Manoj Shinde', 'User', 'Contributor', 'Resource Group', 'rg-payments');
SET IDENTITY_INSERT gov.Identities OFF;

-- gov.IdentityScores: 128 rows
INSERT INTO gov.IdentityScores (AnalysisId, IdentityId, GrantedCount, UsedCount, UnusedHighRisk, RiskScore, SuggestedScope) VALUES
  (1, 1, 1850, 12, 7, 91, 'rg-webapp-prod'),
  (1, 2, 2100, 40, 6, 88, NULL),
  (1, 3, 1850, 95, 5, 71, NULL),
  (1, 4, 210, 60, 0, 48, NULL),
  (1, 5, 320, 280, 0, 12, NULL),
  (1, 6, 140, 120, 0, 8, NULL),
  (1, 7, 260, 70, 3, 55, NULL),
  (1, 8, 1850, 30, 4, 80, NULL),
  (1, 9, 1850, 210, 3, 66, NULL),
  (1, 10, 410, 150, 0, 44, NULL),
  (1, 11, 320, 300, 0, 6, NULL),
  (1, 12, 95, 40, 0, 39, NULL),
  (1, 13, 2100, 25, 7, 84, NULL),
  (1, 14, 180, 110, 0, 33, NULL),
  (1, 15, 1850, 52, 3, 77, NULL),
  (1, 16, 70, 22, 0, 52, NULL),
  (1, 17, 210, 119, 0, 37, NULL),
  (1, 18, 95, 71, 0, 17, NULL),
  (1, 19, 210, 125, 0, 34, NULL),
  (1, 20, 95, 36, 0, 58, NULL),
  (1, 21, 320, 207, 0, 28, NULL),
  (1, 22, 140, 60, 0, 52, NULL),
  (1, 23, 320, 271, 0, 6, NULL),
  (1, 24, 210, 157, 0, 17, NULL),
  (1, 25, 410, 325, 0, 12, NULL),
  (1, 26, 320, 193, 0, 33, NULL),
  (1, 27, 140, 120, 0, 5, NULL),
  (1, 28, 1850, 599, 0, 64, NULL),
  (1, 29, 95, 60, 0, 30, NULL),
  (1, 30, 1850, 1432, 0, 14, NULL),
  (1, 31, 210, 119, 0, 37, NULL),
  (1, 32, 410, 299, 0, 19, NULL),
  (1, 33, 140, 44, 0, 65, NULL),
  (1, 34, 210, 159, 0, 16, NULL),
  (1, 35, 320, 222, 0, 23, NULL),
  (1, 36, 95, 42, 0, 51, NULL),
  (1, 37, 210, 144, 0, 24, NULL),
  (1, 38, 95, 33, 0, 61, NULL),
  (1, 39, 140, 53, 0, 58, NULL),
  (1, 40, 210, 157, 0, 17, NULL),
  (1, 41, 320, 210, 0, 27, NULL),
  (1, 42, 410, 210, 0, 43, NULL),
  (1, 43, 95, 72, 0, 16, NULL),
  (1, 44, 210, 176, 0, 7, NULL),
  (1, 45, 95, 62, 0, 28, NULL),
  (1, 46, 410, 210, 0, 43, NULL),
  (1, 47, 1850, 1299, 0, 22, NULL),
  (1, 48, 210, 127, 0, 33, NULL),
  (1, 49, 210, 85, 0, 55, NULL),
  (1, 50, 210, 132, 0, 30, NULL),
  (1, 51, 140, 105, 0, 17, NULL),
  (1, 52, 410, 332, 0, 10, NULL),
  (1, 53, 140, 62, 0, 51, NULL),
  (1, 54, 1850, 616, 0, 63, NULL),
  (1, 55, 1850, 716, 0, 57, NULL),
  (1, 56, 410, 214, 0, 42, NULL),
  (1, 57, 320, 279, 0, 3, NULL),
  (1, 58, 140, 96, 0, 24, NULL),
  (1, 59, 140, 92, 0, 27, NULL),
  (1, 60, 410, 188, 0, 49, NULL),
  (1, 61, 140, 47, 0, 63, NULL),
  (1, 62, 95, 64, 0, 25, NULL),
  (1, 63, 320, 202, 0, 30, NULL),
  (1, 64, 95, 30, 0, 65, NULL),
  (1, 65, 210, 81, 0, 57, NULL),
  (1, 66, 320, 196, 0, 32, NULL),
  (1, 67, 95, 41, 0, 52, NULL),
  (1, 68, 1850, 1365, 0, 18, NULL),
  (1, 69, 95, 76, 0, 11, NULL),
  (1, 70, 410, 199, 0, 46, NULL),
  (1, 71, 320, 124, 0, 57, NULL),
  (1, 72, 140, 57, 0, 55, NULL),
  (1, 73, 140, 121, 0, 4, NULL),
  (1, 74, 95, 83, 0, 3, NULL),
  (1, 75, 320, 130, 0, 55, NULL),
  (1, 76, 1850, 1482, 0, 11, NULL),
  (1, 77, 210, 180, 0, 5, NULL),
  (1, 78, 140, 84, 0, 33, NULL),
  (1, 79, 95, 65, 0, 24, NULL),
  (1, 80, 140, 101, 0, 20, NULL),
  (1, 81, 410, 332, 0, 10, NULL),
  (1, 82, 95, 84, 0, 2, NULL),
  (1, 83, 320, 268, 0, 7, NULL),
  (1, 84, 210, 174, 0, 8, NULL),
  (1, 85, 210, 136, 0, 28, NULL),
  (1, 86, 95, 43, 0, 50, NULL),
  (1, 87, 210, 96, 0, 49, NULL),
  (1, 88, 210, 74, 0, 61, NULL),
  (1, 89, 410, 214, 0, 42, NULL),
  (1, 90, 140, 95, 0, 25, NULL),
  (1, 91, 95, 36, 0, 58, NULL),
  (1, 92, 95, 80, 0, 6, NULL),
  (1, 93, 140, 100, 0, 21, NULL),
  (1, 94, 95, 51, 0, 40, NULL),
  (1, 95, 210, 140, 0, 26, NULL),
  (1, 96, 320, 118, 0, 59, NULL),
  (1, 97, 320, 207, 0, 28, NULL),
  (1, 98, 95, 35, 0, 59, NULL),
  (1, 99, 320, 193, 0, 33, NULL),
  (1, 100, 1850, 916, 0, 45, NULL),
  (1, 101, 210, 70, 0, 63, NULL),
  (1, 102, 1850, 1199, 0, 28, NULL),
  (1, 103, 210, 102, 0, 46, NULL),
  (1, 104, 410, 148, 0, 60, NULL),
  (1, 105, 320, 101, 0, 65, NULL),
  (1, 106, 320, 130, 0, 55, NULL),
  (1, 107, 210, 178, 0, 6, NULL),
  (1, 108, 1850, 1049, 0, 37, NULL),
  (1, 109, 320, 210, 0, 27, NULL),
  (1, 110, 410, 177, 0, 52, NULL),
  (1, 111, 320, 202, 0, 30, NULL),
  (1, 112, 1850, 866, 0, 48, NULL),
  (1, 113, 95, 64, 0, 25, NULL),
  (1, 114, 210, 66, 0, 65, NULL),
  (1, 115, 320, 256, 0, 11, NULL),
  (1, 116, 410, 325, 0, 12, NULL),
  (1, 117, 140, 82, 0, 35, NULL),
  (1, 118, 140, 122, 0, 3, NULL),
  (1, 119, 1850, 1099, 0, 34, NULL),
  (1, 120, 95, 63, 0, 26, NULL),
  (1, 121, 140, 71, 0, 44, NULL),
  (1, 122, 140, 64, 0, 49, NULL),
  (1, 123, 140, 50, 0, 60, NULL),
  (1, 124, 320, 210, 0, 27, NULL),
  (1, 125, 410, 321, 0, 13, NULL),
  (1, 126, 210, 136, 0, 28, NULL),
  (1, 127, 1850, 1032, 0, 38, NULL),
  (1, 128, 95, 75, 0, 12, NULL);

-- gov.PermissionUsage: 12 rows
INSERT INTO gov.PermissionUsage (AnalysisId, IdentityId, Action, FirstUsed, LastUsed, EventCount, ResourceGroup) VALUES
  (1, 1, 'Microsoft.Web/sites/write', '2026-08-14', '2026-11-09', 46, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/sites/config/write', '2026-08-14', '2026-11-09', 31, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/sites/slots/write', '2026-08-20', '2026-11-08', 18, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/sites/restart/action', '2026-09-01', '2026-11-07', 9, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Resources/deployments/write', '2026-08-14', '2026-11-09', 52, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/sites/read', '2026-08-14', '2026-11-10', 212, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/sites/config/read', '2026-08-14', '2026-11-10', 140, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/serverfarms/read', '2026-08-15', '2026-11-09', 64, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Insights/components/read', '2026-08-18', '2026-11-09', 27, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Resources/deployments/read', '2026-08-14', '2026-11-09', 52, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Web/sites/slots/read', '2026-08-20', '2026-11-08', 18, 'rg-webapp-prod'),
  (1, 1, 'Microsoft.Resources/subscriptions/resourceGroups/read', '2026-08-14', '2026-11-10', 88, 'rg-webapp-prod');

-- gov.Findings: 37 rows
SET IDENTITY_INSERT gov.Findings ON;
INSERT INTO gov.Findings (FindingId, AnalysisId, IdentityId, Action, Category, Severity, GrantedByRole, LastUsed, Status) VALUES
  (1, 1, 1, 'Microsoft.Compute/virtualMachines/delete', 'Delete', 'HIGH', 'Contributor', NULL, 'Open'),
  (2, 1, 1, 'Microsoft.Network/networkSecurityGroups/write', 'Network', 'HIGH', 'Contributor', NULL, 'Open'),
  (3, 1, 2, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'HIGH', 'Owner', NULL, 'Open'),
  (4, 1, 2, 'Microsoft.KeyVault/vaults/write', 'Key Vault', 'HIGH', 'Owner', NULL, 'Open'),
  (5, 1, 3, 'Microsoft.Sql/servers/databases/delete', 'Delete', 'HIGH', 'Contributor', NULL, 'Open'),
  (6, 1, 8, 'Microsoft.Storage/storageAccounts/delete', 'Delete', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (7, 1, 1, 'Microsoft.Compute/disks/delete', 'Delete', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (8, 1, 2, 'Microsoft.Network/virtualNetworks/delete', 'Delete', 'HIGH', 'Owner', NULL, 'Open'),
  (9, 1, 3, 'Microsoft.Network/networkSecurityGroups/securityRules/write', 'Network', 'HIGH', 'Contributor', NULL, 'Open'),
  (10, 1, 8, 'Microsoft.KeyVault/vaults/secrets/write', 'Key Vault', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (11, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'HIGH', 'Owner', NULL, 'Open'),
  (12, 1, 15, 'Microsoft.Web/sites/delete', 'Delete', 'HIGH', 'Contributor', NULL, 'Open'),
  (13, 1, 9, 'Microsoft.KeyVault/vaults/delete', 'Key Vault', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (14, 1, 7, 'Microsoft.Network/networkSecurityGroups/delete', 'Network', 'HIGH', 'Network Contributor', NULL, 'Open'),
  (15, 1, 1, 'Microsoft.Compute/disks/delete', 'Delete', 'HIGH', 'Contributor', NULL, 'Open'),
  (16, 1, 2, 'Microsoft.Network/virtualNetworks/delete', 'Delete', 'MEDIUM', 'Owner', NULL, 'Open'),
  (17, 1, 3, 'Microsoft.Network/networkSecurityGroups/securityRules/write', 'Network', 'HIGH', 'Contributor', NULL, 'Open'),
  (18, 1, 8, 'Microsoft.KeyVault/vaults/secrets/write', 'Key Vault', 'HIGH', 'Contributor', NULL, 'Open'),
  (19, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'MEDIUM', 'Owner', NULL, 'Open'),
  (20, 1, 15, 'Microsoft.Web/sites/delete', 'Delete', 'HIGH', 'Contributor', NULL, 'Open'),
  (21, 1, 9, 'Microsoft.KeyVault/vaults/delete', 'Key Vault', 'HIGH', 'Contributor', NULL, 'Open'),
  (22, 1, 7, 'Microsoft.Network/networkSecurityGroups/delete', 'Network', 'MEDIUM', 'Network Contributor', NULL, 'Open'),
  (23, 1, 1, 'Microsoft.Compute/disks/delete', 'Delete', 'HIGH', 'Contributor', NULL, 'Open'),
  (24, 1, 2, 'Microsoft.Network/virtualNetworks/delete', 'Delete', 'HIGH', 'Owner', NULL, 'Open'),
  (25, 1, 3, 'Microsoft.Network/networkSecurityGroups/securityRules/write', 'Network', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (26, 1, 8, 'Microsoft.KeyVault/vaults/secrets/write', 'Key Vault', 'HIGH', 'Contributor', NULL, 'Open'),
  (27, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'HIGH', 'Owner', NULL, 'Open'),
  (28, 1, 15, 'Microsoft.Web/sites/delete', 'Delete', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (29, 1, 9, 'Microsoft.KeyVault/vaults/delete', 'Key Vault', 'HIGH', 'Contributor', NULL, 'Open'),
  (30, 1, 7, 'Microsoft.Network/networkSecurityGroups/delete', 'Network', 'HIGH', 'Network Contributor', NULL, 'Open'),
  (31, 1, 1, 'Microsoft.Compute/disks/delete', 'Delete', 'MEDIUM', 'Contributor', NULL, 'Open'),
  (32, 1, 2, 'Microsoft.Network/virtualNetworks/delete', 'Delete', 'HIGH', 'Owner', NULL, 'Open'),
  (33, 1, 3, 'Microsoft.Network/networkSecurityGroups/securityRules/write', 'Network', 'HIGH', 'Contributor', NULL, 'Open'),
  (34, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'HIGH', 'Owner', NULL, 'Open'),
  (35, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'MEDIUM', 'Owner', NULL, 'Open'),
  (36, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'HIGH', 'Owner', NULL, 'Open'),
  (37, 1, 13, 'Microsoft.Authorization/roleAssignments/write', 'Role Assignment', 'HIGH', 'Owner', NULL, 'Open');
SET IDENTITY_INSERT gov.Findings OFF;

-- gov.GeneratedRoles: 5 rows
SET IDENTITY_INSERT gov.GeneratedRoles ON;
INSERT INTO gov.GeneratedRoles (GeneratedRoleId, AnalysisId, IdentityId, RoleName, OriginalRole, AssignableScope, IncludeBaselineRead, IsReviewed, ReviewedBy, ReviewedAt) VALUES
  (1, 1, 1, 'svc-webapp-deploy-custom-role', 'Contributor (Subscription)', 'rg-webapp-prod', 1, 0, NULL, NULL),
  (2, 1, 2, 'rahul.verma-custom-role', 'Owner (Subscription)', 'rg-finance', 1, 0, NULL, NULL),
  (3, 1, 3, 'priya.sharma-custom-role', 'Contributor (rg-sales)', 'rg-sales', 1, 0, NULL, NULL),
  (4, 1, 4, 'svc-data-pipeline-custom-role', 'VM Contributor (rg-data)', 'rg-data', 1, 1, 25, '2026-11-11'),
  (5, 1, 8, 'mi-reports-func-custom-role', 'Contributor (rg-reports)', 'rg-reports', 1, 0, NULL, NULL);
SET IDENTITY_INSERT gov.GeneratedRoles OFF;

-- gov.GeneratedRoleActions: 16 rows
INSERT INTO gov.GeneratedRoleActions (GeneratedRoleId, Action, IsRare, Note) VALUES
  (1, 'Microsoft.Web/sites/write', 0, NULL),
  (1, 'Microsoft.Web/sites/config/write', 0, NULL),
  (1, 'Microsoft.Web/sites/slots/write', 0, NULL),
  (1, 'Microsoft.Web/sites/restart/action', 0, NULL),
  (1, 'Microsoft.Resources/deployments/write', 0, NULL),
  (1, 'Microsoft.Web/sites/read', 0, NULL),
  (1, 'Microsoft.Web/sites/config/read', 0, NULL),
  (1, 'Microsoft.Web/serverfarms/read', 0, NULL),
  (1, 'Microsoft.Insights/components/read', 0, NULL),
  (1, 'Microsoft.Resources/deployments/read', 0, NULL),
  (1, 'Microsoft.Web/sites/slots/read', 0, NULL),
  (1, 'Microsoft.Resources/subscriptions/resourceGroups/read', 0, NULL),
  (1, 'Microsoft.Web/sites/delete', 1, 'used once, 85 days ago'),
  (1, 'Microsoft.Web/sites/backup/action', 1, 'used once, 70 days ago'),
  (2, 'Microsoft.Authorization/policyAssignments/write', 1, 'used once, 60 days ago'),
  (4, 'Microsoft.Compute/virtualMachines/deallocate/action', 1, 'used twice, 40 days ago');

-- gov.RuleSettings: 1 rows
INSERT INTO gov.RuleSettings (WeightUnused, WeightHighRisk, WeightScope, DefaultPeriodDays, RareThreshold, IncludeBaselineRead, IsActive, UpdatedBy) VALUES
  (50, 30, 20, 90, 3, 1, 1, 25);

-- gov.HighRiskPatterns: 4 rows
INSERT INTO gov.HighRiskPatterns (Pattern, IsEnabled) VALUES
  ('*/delete', 1),
  ('Microsoft.Authorization/*/write', 1),
  ('Microsoft.KeyVault/*', 1),
  ('Microsoft.Network/networkSecurityGroups/write', 1);

-- gov.Reports: 4 rows
INSERT INTO gov.Reports (Code, AnalysisId, IdentityCount, AvgScore, HighRiskCount, GeneratedBy, GeneratedAt) VALUES
  ('RPT-1110', 1, 128, 54, 37, 25, '2026-11-10'),
  ('RPT-1103', 2, 126, 57, 41, 25, '2026-11-03'),
  ('RPT-1027', 3, 74, 49, 21, 25, '2026-10-27'),
  ('RPT-1020', 4, 91, 62, 30, 25, '2026-10-20');

-- gov.Notifications: 3 rows
INSERT INTO gov.Notifications (UserId, Message, IsRead, CreatedAt) VALUES
  (25, 'Analysis AN-2026-1110 completed for Northwind Traders', 0, DATEADD(DAY, -0, SYSUTCDATETIME())),
  (25, '4 generated roles are waiting for review', 1, DATEADD(DAY, -1, SYSUTCDATETIME())),
  (25, '7 new high-risk unused permissions found', 1, DATEADD(DAY, -2, SYSUTCDATETIME()));

-- emdt.Enterprises: 3 rows
SET IDENTITY_INSERT emdt.Enterprises ON;
INSERT INTO emdt.Enterprises (EnterpriseId, Name, Industry, City, ContactName, ContactEmail, ContactPhone, RegisteredBy, RegisteredAt) VALUES
  (1, 'Northwind Bank', 'Banking', 'Mumbai', 'Ravi Menon', 'ravi.menon@northwindbank.com', '9820012345', 12, '2026-09-15'),
  (2, 'Contoso Retail', 'Retail', 'Pune', 'Neha Bansal', 'neha.bansal@contosoretail.com', '9876501234', 12, '2026-10-02'),
  (3, 'Fabrikam Health', 'Healthcare', 'Bengaluru', 'Arjun Rao', 'arjun.rao@fabrikamhealth.com', '9988776655', 12, '2026-11-08');
SET IDENTITY_INSERT emdt.Enterprises OFF;

-- emdt.Classifications: 2 rows
SET IDENTITY_INSERT emdt.Classifications ON;
INSERT INTO emdt.Classifications (ClassificationId, EnterpriseId, TotalApps, Dependencies, Apis, DatabaseCount, StorageGB, Environments, BusinessUnits, UserCount, CritLow, CritMedium, CritHigh, CritCritical, CloudType, LinesOfCode, EmsiScore, ClassifiedBy, ClassifiedAt) VALUES
  (1, 1, 120, 180, 60, 22, 8000, 4, 8, 850, 10, 30, 50, 30, 'Hybrid Cloud', 4500000, 77.87, 12, '2026-11-10'),
  (2, 2, 40, 60, 25, 8, 900, 3, 3, 300, 10, 15, 10, 5, 'Public Cloud', 800000, 44.91, 12, '2026-10-20');
SET IDENTITY_INSERT emdt.Classifications OFF;

-- emdt.ClassificationTechnologies: 5 rows
INSERT INTO emdt.ClassificationTechnologies (ClassificationId, Technology) VALUES
  (1, 'Java'),
  (1, '.NET'),
  (1, 'COBOL'),
  (2, 'Java'),
  (2, 'PHP');

-- emdt.ClassificationPolicies: 6 rows
INSERT INTO emdt.ClassificationPolicies (ClassificationId, PolicyName) VALUES
  (1, 'GDPR'),
  (1, 'PCI DSS'),
  (1, 'ISO 27001'),
  (1, 'SOC 2'),
  (2, 'PCI DSS'),
  (2, 'ISO 27001');

-- emdt.Applications: 10 rows
SET IDENTITY_INSERT emdt.Applications ON;
INSERT INTO emdt.Applications (ApplicationId, EnterpriseId, Name, Technology, Framework, SupportStatus, AgeYears, Criticality, HasSensitiveData, SecurityIssues, DowntimeTolerance, CompliancePolicies, Deployment, DocQuality, Complexity, LinesOfCode, Status, BusinessUnit, OwnerTeam) VALUES
  (1, 1, 'Trade Finance Portal', '.NET', '.NET Framework 4.5', 'Unsupported', 14, 'Critical', 1, 4, 'Low', 2, 'On-Premise', 'Poor', 'High', 1200000, 'Legacy', 'Corporate Banking', 'Trade Team'),
  (2, 1, 'Customer360', '.NET', '.NET 6', 'Extended', 6, 'High', 1, 2, 'Medium', 2, 'Private Cloud', 'Average', 'Medium', 450000, 'Active', 'Retail Banking', 'Digital Team'),
  (3, 1, 'Payment Gateway', 'Java', 'Java 11', 'Supported', 7, 'Critical', 1, 3, 'Low', 3, 'Hybrid Cloud', 'Average', 'High', 900000, 'Active', 'Payments', 'Payments Team'),
  (4, 1, 'Core Banking', 'COBOL', 'COBOL 85', 'Unsupported', 22, 'Critical', 1, 6, 'Low', 4, 'On-Premise', 'Poor', 'High', 3500000, 'Legacy', 'Core Banking', 'Mainframe Team'),
  (5, 1, 'Reporting Hub', 'Java', 'Java 8', 'Extended', 9, 'Medium', 0, 1, 'High', 1, 'Private Cloud', 'Good', 'Medium', 300000, 'Active', 'Analytics', 'Data Team'),
  (6, 1, 'HR System', 'PHP', 'PHP 7.2', 'Unsupported', 11, 'High', 1, 3, 'Medium', 2, 'On-Premise', 'Average', 'Medium', 250000, 'Legacy', 'Human Resources', 'HR IT'),
  (7, 2, 'Online Store', 'Java', 'Java 17', 'Supported', 4, 'Critical', 1, 2, 'Low', 1, 'Public Cloud', 'Good', 'Medium', 600000, 'Active', 'E-commerce', 'Web Team'),
  (8, 2, 'Inventory Manager', 'PHP', 'PHP 8.1', 'Supported', 5, 'High', 0, 1, 'Medium', 1, 'Public Cloud', 'Good', 'Low', 200000, 'Active', 'Operations', 'Ops IT'),
  (9, 2, 'Loyalty Program', 'PHP', 'PHP 5.6', 'Unsupported', 10, 'Medium', 1, 4, 'High', 1, 'On-Premise', 'Poor', 'Medium', 150000, 'Legacy', 'Marketing', 'CRM Team'),
  (10, 2, 'Supplier Portal', 'Java', 'Java 8', 'Extended', 8, 'Medium', 0, 2, 'High', 1, 'Hybrid Cloud', 'Average', 'Low', 180000, 'Active', 'Procurement', 'Vendor IT');
SET IDENTITY_INSERT emdt.Applications OFF;

-- emdt.Components: 19 rows
SET IDENTITY_INSERT emdt.Components ON;
INSERT INTO emdt.Components (ComponentId, EnterpriseId, ApplicationId, ComponentType, Name) VALUES
  (1, 1, 2, 'Service', 'Authentication Service'),
  (2, 1, 5, 'API', 'Reporting API'),
  (3, 1, 2, 'Service', 'Notification Service'),
  (4, 1, 1, 'Database', 'Trade Database'),
  (5, 1, 1, 'Service', 'Trade Service'),
  (6, 1, 2, 'API', 'Customer API'),
  (7, 1, 2, 'Database', 'Customer Database'),
  (8, 1, 3, 'Service', 'Payment Service'),
  (9, 1, 3, 'API', 'Payment API'),
  (10, 1, 4, 'Database', 'Core Ledger Database'),
  (11, 1, 4, 'API', 'Banking API'),
  (12, 1, 5, 'Database', 'Reports Database'),
  (13, 1, 6, 'Service', 'Payroll Service'),
  (14, 1, 6, 'Database', 'HR Database'),
  (15, 2, 7, 'Service', 'Checkout Service'),
  (16, 2, 8, 'API', 'Product API'),
  (17, 2, 7, 'Database', 'Orders Database'),
  (18, 2, 8, 'Database', 'Stock Database'),
  (19, 2, 9, 'Service', 'Points Service');
SET IDENTITY_INSERT emdt.Components OFF;

-- emdt.Dependencies: 20 rows
SET IDENTITY_INSERT emdt.Dependencies ON;
INSERT INTO emdt.Dependencies (DependencyId, EnterpriseId, SourceApplicationId, SourceComponentId, TargetApplicationId, TargetComponentId, Criticality, Description, CreatedBy) VALUES
  (1, 1, 1, NULL, NULL, 1, 'Critical', 'Login and tokens', 12),
  (2, 1, 1, NULL, NULL, 2, 'High', 'Trade reports', 12),
  (3, 1, 1, NULL, NULL, 3, 'Medium', 'Status alerts', 12),
  (4, 1, NULL, 5, NULL, 4, 'Critical', 'Trade records', 12),
  (5, 1, NULL, 6, NULL, 7, 'Critical', 'Customer master data', 12),
  (6, 1, 2, NULL, NULL, 11, 'High', 'Account balances', 12),
  (7, 1, NULL, 8, NULL, 6, 'High', 'Customer lookup', 12),
  (8, 1, NULL, 8, NULL, 7, 'Critical', 'Customer records', 12),
  (9, 1, NULL, 9, NULL, 11, 'Critical', 'Ledger posting', 12),
  (10, 1, NULL, 8, NULL, 1, 'High', 'Payment authorisation', 12),
  (11, 1, 4, NULL, NULL, 10, 'Critical', 'General ledger', 12),
  (12, 1, NULL, 2, NULL, 12, 'Medium', 'Report storage', 12),
  (13, 1, 5, NULL, 4, NULL, 'High', 'Nightly data extract', 12),
  (14, 1, NULL, 13, NULL, 14, 'High', 'Employee records', 12),
  (15, 1, 6, NULL, NULL, 1, 'Medium', 'Single sign-on', 12),
  (16, 2, NULL, 15, NULL, 17, 'Critical', 'Order storage', 12),
  (17, 2, NULL, 15, NULL, 16, 'High', 'Product prices', 12),
  (18, 2, NULL, 16, NULL, 18, 'Medium', 'Stock levels', 12),
  (19, 2, 9, NULL, NULL, 15, 'Low', 'Reward on checkout', 12),
  (20, 2, 10, NULL, NULL, 16, 'Medium', 'Supplier catalogue', 12);
SET IDENTITY_INSERT emdt.Dependencies OFF;

-- emdt.Assessments: 5 rows
SET IDENTITY_INSERT emdt.Assessments ON;
INSERT INTO emdt.Assessments (AssessmentId, ApplicationId, TechnicalDebt, GovernanceRisk, DependencyImpact, AssessedBy, AssessedAt) VALUES
  (1, 1, 95, 90, 83, 13, '2026-11-10'),
  (2, 3, 52, 95, 83, 13, '2026-11-08'),
  (3, 5, 45, 32, 59, 13, '2026-11-06'),
  (4, 7, 28, 85, 59, 13, '2026-11-04'),
  (5, 6, 70, 72, 53, 13, '2026-11-03');
SET IDENTITY_INSERT emdt.Assessments OFF;

-- emdt.AssessmentFactors: 55 rows
INSERT INTO emdt.AssessmentFactors (AssessmentId, ScoreType, FactorName, FactorValue, Points) VALUES
  (1, 'TechnicalDebt', 'Application age', '14 years', 25),
  (1, 'TechnicalDebt', 'Framework support', '.NET Framework 4.5 (Unsupported)', 25),
  (1, 'TechnicalDebt', 'Documentation quality', 'Poor', 15),
  (1, 'TechnicalDebt', 'Code complexity', 'High', 20),
  (1, 'TechnicalDebt', 'Codebase size', '12,00,000 lines', 10),
  (1, 'GovernanceRisk', 'Business criticality', 'Critical', 30),
  (1, 'GovernanceRisk', 'Sensitive data', 'Yes', 20),
  (1, 'GovernanceRisk', 'Security issues', '4 issues', 10),
  (1, 'GovernanceRisk', 'Dependency criticality', 'Highest: Critical', 15),
  (1, 'GovernanceRisk', 'Downtime tolerance', 'Low', 10),
  (1, 'GovernanceRisk', 'Compliance requirements', '2 policies', 5),
  (2, 'TechnicalDebt', 'Application age', '7 years', 14),
  (2, 'TechnicalDebt', 'Framework support', 'Java 11 (Supported)', 0),
  (2, 'TechnicalDebt', 'Documentation quality', 'Average', 8),
  (2, 'TechnicalDebt', 'Code complexity', 'High', 20),
  (2, 'TechnicalDebt', 'Codebase size', '9,00,000 lines', 10),
  (2, 'GovernanceRisk', 'Business criticality', 'Critical', 30),
  (2, 'GovernanceRisk', 'Sensitive data', 'Yes', 20),
  (2, 'GovernanceRisk', 'Security issues', '3 issues', 10),
  (2, 'GovernanceRisk', 'Dependency criticality', 'Highest: Critical', 15),
  (2, 'GovernanceRisk', 'Downtime tolerance', 'Low', 10),
  (2, 'GovernanceRisk', 'Compliance requirements', '3 policies', 10),
  (3, 'TechnicalDebt', 'Application age', '9 years', 18),
  (3, 'TechnicalDebt', 'Framework support', 'Java 8 (Extended)', 12),
  (3, 'TechnicalDebt', 'Documentation quality', 'Good', 0),
  (3, 'TechnicalDebt', 'Code complexity', 'Medium', 10),
  (3, 'TechnicalDebt', 'Codebase size', '3,00,000 lines', 5),
  (3, 'GovernanceRisk', 'Business criticality', 'Medium', 12),
  (3, 'GovernanceRisk', 'Sensitive data', 'No', 0),
  (3, 'GovernanceRisk', 'Security issues', '1 issues', 5),
  (3, 'GovernanceRisk', 'Dependency criticality', 'Highest: High', 10),
  (3, 'GovernanceRisk', 'Downtime tolerance', 'High', 0),
  (3, 'GovernanceRisk', 'Compliance requirements', '1 policy', 5),
  (4, 'TechnicalDebt', 'Application age', '4 years', 8),
  (4, 'TechnicalDebt', 'Framework support', 'Java 17 (Supported)', 0),
  (4, 'TechnicalDebt', 'Documentation quality', 'Good', 0),
  (4, 'TechnicalDebt', 'Code complexity', 'Medium', 10),
  (4, 'TechnicalDebt', 'Codebase size', '6,00,000 lines', 10),
  (4, 'GovernanceRisk', 'Business criticality', 'Critical', 30),
  (4, 'GovernanceRisk', 'Sensitive data', 'Yes', 20),
  (4, 'GovernanceRisk', 'Security issues', '2 issues', 5),
  (4, 'GovernanceRisk', 'Dependency criticality', 'Highest: Critical', 15),
  (4, 'GovernanceRisk', 'Downtime tolerance', 'Low', 10),
  (4, 'GovernanceRisk', 'Compliance requirements', '1 policy', 5),
  (5, 'TechnicalDebt', 'Application age', '11 years', 22),
  (5, 'TechnicalDebt', 'Framework support', 'PHP 7.2 (Unsupported)', 25),
  (5, 'TechnicalDebt', 'Documentation quality', 'Average', 8),
  (5, 'TechnicalDebt', 'Code complexity', 'Medium', 10),
  (5, 'TechnicalDebt', 'Codebase size', '2,50,000 lines', 5),
  (5, 'GovernanceRisk', 'Business criticality', 'High', 22),
  (5, 'GovernanceRisk', 'Sensitive data', 'Yes', 20),
  (5, 'GovernanceRisk', 'Security issues', '3 issues', 10),
  (5, 'GovernanceRisk', 'Dependency criticality', 'Highest: High', 10),
  (5, 'GovernanceRisk', 'Downtime tolerance', 'Medium', 5),
  (5, 'GovernanceRisk', 'Compliance requirements', '2 policies', 5);

-- emdt.Simulations: 6 rows
SET IDENTITY_INSERT emdt.Simulations ON;
INSERT INTO emdt.Simulations (SimulationId, ApplicationId, AssessmentId, Strategy, StrategyRisk, EmsiScore, ModernizationRisk, EffortWeeks, TeamSize, CostLakh, Recommendation, CreatedBy, CreatedAt) VALUES
  (1, 1, 1, 'Refactor', 75, 77.87, 87, 22, 5, 22.15, 'High modernization risk. Use a phased implementation with a parallel run. Stabilise the critical dependencies first.', 13, '2026-11-10'),
  (2, 3, 2, 'Replatform', 50, 77.87, 70, 18, 4, 15.5, 'High modernization risk. Use a phased implementation with a parallel run. Stabilise the critical dependencies first.', 13, '2026-11-08'),
  (3, 5, 3, 'Rehost', 25, 77.87, 41, 13, 3, 9.71, 'Moderate risk. Proceed in planned releases with dependency testing at every step.', 13, '2026-11-06'),
  (4, 7, 4, 'Replatform', 50, 44.91, 54, 14, 4, 12.68, 'Moderate risk. Proceed in planned releases with dependency testing at every step.', 13, '2026-11-04'),
  (5, 6, 5, 'Rebuild', 100, 77.87, 72, 22, 6, 26.01, 'High modernization risk. Use a phased implementation with a parallel run. Stabilise the critical dependencies first.', 13, '2026-11-03'),
  (6, 3, 2, 'Refactor', 75, 77.87, 75, 21, 5, 21.29, 'High modernization risk. Use a phased implementation with a parallel run. Stabilise the critical dependencies first.', 13, '2026-11-09');
SET IDENTITY_INSERT emdt.Simulations OFF;

-- emdt.SimulationImpacts: 18 rows
INSERT INTO emdt.SimulationImpacts (SimulationId, SequenceNo, AffectedName, AffectedType, Criticality, OwnerName) VALUES
  (1, 1, 'Authentication Service', 'Service', 'Critical', 'Customer360'),
  (1, 2, 'Reporting API', 'API', 'High', 'Reporting Hub'),
  (1, 3, 'Notification Service', 'Service', 'Medium', 'Customer360'),
  (1, 4, 'Trade Database', 'Database', 'Critical', 'Same application'),
  (2, 1, 'Customer API', 'API', 'High', 'Customer360'),
  (2, 2, 'Customer Database', 'Database', 'Critical', 'Customer360'),
  (2, 3, 'Banking API', 'API', 'Critical', 'Core Banking'),
  (2, 4, 'Authentication Service', 'Service', 'High', 'Customer360'),
  (3, 1, 'Reports Database', 'Database', 'Medium', 'Same application'),
  (3, 2, 'Core Banking', 'Application', 'High', 'Core Banking'),
  (4, 1, 'Orders Database', 'Database', 'Critical', 'Same application'),
  (4, 2, 'Product API', 'API', 'High', 'Inventory Manager'),
  (5, 1, 'HR Database', 'Database', 'High', 'Same application'),
  (5, 2, 'Authentication Service', 'Service', 'Medium', 'Customer360'),
  (6, 1, 'Customer API', 'API', 'High', 'Customer360'),
  (6, 2, 'Customer Database', 'Database', 'Critical', 'Customer360'),
  (6, 3, 'Banking API', 'API', 'Critical', 'Core Banking'),
  (6, 4, 'Authentication Service', 'Service', 'High', 'Customer360');

-- emdt.Plans: 6 rows
SET IDENTITY_INSERT emdt.Plans ON;
INSERT INTO emdt.Plans (PlanId, SimulationId, PlanName, Status, Notes, CreatedBy, CreatedAt, SubmittedAt, DecidedBy, DecidedAt, ExecutiveComment) VALUES
  (1, 1, 'Trade Finance Portal Refactor Plan', 'Submitted', 'Refactoring is recommended because the application has high technical debt, critical dependencies and an unsupported framework. Use phased implementation.', 13, '2026-11-10', '2026-11-10', NULL, NULL, NULL),
  (2, 2, 'Payment Gateway Replatform Plan', 'Submitted', 'Move the gateway to a managed Java runtime before the PCI audit.', 13, '2026-11-08', '2026-11-08', NULL, NULL, NULL),
  (3, 3, 'Reporting Hub Rehost Plan', 'Submitted', 'Low-risk quick win - lift and shift reporting.', 13, '2026-11-06', '2026-11-07', NULL, NULL, NULL),
  (4, 4, 'Online Store Replatform Plan', 'Approved', NULL, 13, '2026-11-04', '2026-11-04', 14, '2026-11-04', 'Approved. Start after the festive sale.'),
  (5, 5, 'HR System Rebuild Plan', 'Rejected', 'Full rebuild of the HR system.', 13, '2026-11-03', '2026-11-03', 14, '2026-11-03', 'Too costly for this year. Evaluate a SaaS HR product first.'),
  (6, 6, 'Payment Gateway Refactor Plan', 'Draft', 'Alternative option to compare with PLAN-002.', 13, '2026-11-09', NULL, NULL, NULL, NULL);
SET IDENTITY_INSERT emdt.Plans OFF;

-- emdt.PlanPhases: 24 rows
INSERT INTO emdt.PlanPhases (PlanId, PhaseNo, PhaseName, Weeks) VALUES
  (1, 1, 'Assessment and Readiness', 3),
  (1, 2, 'Architecture and Design', 4),
  (1, 3, 'Implementation', 10),
  (1, 4, 'Testing and Validation', 5),
  (2, 1, 'Assessment and Readiness', 3),
  (2, 2, 'Architecture and Design', 4),
  (2, 3, 'Implementation', 8),
  (2, 4, 'Testing and Validation', 3),
  (3, 1, 'Assessment and Readiness', 2),
  (3, 2, 'Architecture and Design', 3),
  (3, 3, 'Implementation', 6),
  (3, 4, 'Testing and Validation', 2),
  (4, 1, 'Assessment and Readiness', 2),
  (4, 2, 'Architecture and Design', 3),
  (4, 3, 'Implementation', 6),
  (4, 4, 'Testing and Validation', 3),
  (5, 1, 'Assessment and Readiness', 3),
  (5, 2, 'Architecture and Design', 4),
  (5, 3, 'Implementation', 10),
  (5, 4, 'Testing and Validation', 5),
  (6, 1, 'Assessment and Readiness', 3),
  (6, 2, 'Architecture and Design', 4),
  (6, 3, 'Implementation', 9),
  (6, 4, 'Testing and Validation', 5);

COMMIT TRANSACTION;
GO

/* ---------- Quick check: row counts ---------- */
SELECT 'auth.Users' AS TableName, COUNT(*) AS Rows FROM auth.Users UNION ALL
SELECT 'auth.AuditLogs', COUNT(*) FROM auth.AuditLogs UNION ALL
SELECT 'gov.Companies', COUNT(*) FROM gov.Companies UNION ALL
SELECT 'gov.Analyses', COUNT(*) FROM gov.Analyses UNION ALL
SELECT 'gov.Identities', COUNT(*) FROM gov.Identities UNION ALL
SELECT 'gov.Findings', COUNT(*) FROM gov.Findings UNION ALL
SELECT 'gov.GeneratedRoles', COUNT(*) FROM gov.GeneratedRoles UNION ALL
SELECT 'emdt.Enterprises', COUNT(*) FROM emdt.Enterprises UNION ALL
SELECT 'emdt.Applications', COUNT(*) FROM emdt.Applications UNION ALL
SELECT 'emdt.Dependencies', COUNT(*) FROM emdt.Dependencies UNION ALL
SELECT 'emdt.Assessments', COUNT(*) FROM emdt.Assessments UNION ALL
SELECT 'emdt.Simulations', COUNT(*) FROM emdt.Simulations UNION ALL
SELECT 'emdt.Plans', COUNT(*) FROM emdt.Plans;

-- Example: what the Executive Approvals page shows
SELECT PlanCode, PlanName, Status, ApplicationName, Strategy, EffortWeeks, CostLakh FROM emdt.vw_PlanSummary ORDER BY PlanCode;
GO
