/* =====================================================================
   SecureShift - 03_verify_dev.sql   (READ-ONLY: changes nothing)

   Checks that schema dev has everything 01 + 02 create:
     28 tables, 4 views, 46 foreign keys, 569 rows (fresh seed).
   The last result line says  VERIFY RESULT: PASS  or  VERIFY RESULT: FAIL.

   How to run (psql):
     psql "<connection>" -v ON_ERROR_STOP=1 -f 03_verify_dev.sql
   On the faculty DB the row counts change once people use the app;
   then only the schema check (tables / views / FKs) has to say PASS.
   ===================================================================== */

BEGIN TRANSACTION READ ONLY;

-- 1. Where am I connected?
SELECT current_database() AS database_name,
       current_user       AS user_name,
       COALESCE(host(inet_server_addr()), 'local socket') AS server,
       current_setting('server_version') AS postgres_version;

-- 2. Rows per table (expected = fresh seed from 02)
WITH expected(table_name, expected_rows) AS (VALUES
    ('Roles', 5), ('Users', 25), ('AuditLogs', 8),
    ('Companies', 10), ('Analyses', 4), ('Identities', 128), ('IdentityScores', 128),
    ('PermissionUsage', 12), ('Findings', 37), ('GeneratedRoles', 5), ('GeneratedRoleActions', 16),
    ('RuleSettings', 1), ('HighRiskPatterns', 4), ('Reports', 4), ('Notifications', 3),
    ('Enterprises', 3), ('Classifications', 2), ('ClassificationTechnologies', 5), ('ClassificationPolicies', 6),
    ('Applications', 10), ('Components', 19), ('Dependencies', 20), ('Assessments', 5),
    ('AssessmentFactors', 55), ('Simulations', 6), ('SimulationImpacts', 18), ('Plans', 6), ('PlanPhases', 24)
),
actual(table_name, row_count) AS (
              SELECT 'Roles',                      COUNT(*) FROM dev."Roles"
    UNION ALL SELECT 'Users',                      COUNT(*) FROM dev."Users"
    UNION ALL SELECT 'AuditLogs',                  COUNT(*) FROM dev."AuditLogs"
    UNION ALL SELECT 'Companies',                  COUNT(*) FROM dev."Companies"
    UNION ALL SELECT 'Analyses',                   COUNT(*) FROM dev."Analyses"
    UNION ALL SELECT 'Identities',                 COUNT(*) FROM dev."Identities"
    UNION ALL SELECT 'IdentityScores',             COUNT(*) FROM dev."IdentityScores"
    UNION ALL SELECT 'PermissionUsage',            COUNT(*) FROM dev."PermissionUsage"
    UNION ALL SELECT 'Findings',                   COUNT(*) FROM dev."Findings"
    UNION ALL SELECT 'GeneratedRoles',             COUNT(*) FROM dev."GeneratedRoles"
    UNION ALL SELECT 'GeneratedRoleActions',       COUNT(*) FROM dev."GeneratedRoleActions"
    UNION ALL SELECT 'RuleSettings',               COUNT(*) FROM dev."RuleSettings"
    UNION ALL SELECT 'HighRiskPatterns',           COUNT(*) FROM dev."HighRiskPatterns"
    UNION ALL SELECT 'Reports',                    COUNT(*) FROM dev."Reports"
    UNION ALL SELECT 'Notifications',              COUNT(*) FROM dev."Notifications"
    UNION ALL SELECT 'Enterprises',                COUNT(*) FROM dev."Enterprises"
    UNION ALL SELECT 'Classifications',            COUNT(*) FROM dev."Classifications"
    UNION ALL SELECT 'ClassificationTechnologies', COUNT(*) FROM dev."ClassificationTechnologies"
    UNION ALL SELECT 'ClassificationPolicies',     COUNT(*) FROM dev."ClassificationPolicies"
    UNION ALL SELECT 'Applications',               COUNT(*) FROM dev."Applications"
    UNION ALL SELECT 'Components',                 COUNT(*) FROM dev."Components"
    UNION ALL SELECT 'Dependencies',               COUNT(*) FROM dev."Dependencies"
    UNION ALL SELECT 'Assessments',                COUNT(*) FROM dev."Assessments"
    UNION ALL SELECT 'AssessmentFactors',          COUNT(*) FROM dev."AssessmentFactors"
    UNION ALL SELECT 'Simulations',                COUNT(*) FROM dev."Simulations"
    UNION ALL SELECT 'SimulationImpacts',          COUNT(*) FROM dev."SimulationImpacts"
    UNION ALL SELECT 'Plans',                      COUNT(*) FROM dev."Plans"
    UNION ALL SELECT 'PlanPhases',                 COUNT(*) FROM dev."PlanPhases"
)
SELECT table_name, row_count, expected_rows,
       CASE WHEN row_count = expected_rows THEN 'ok' ELSE 'DIFFERENT' END AS check_result
FROM (
    SELECT a.table_name, a.row_count, e.expected_rows, 1 AS sort_group
    FROM actual a JOIN expected e ON e.table_name = a.table_name
    UNION ALL
    SELECT 'TOTAL', (SELECT SUM(row_count) FROM actual), (SELECT SUM(expected_rows) FROM expected), 2
) x
ORDER BY sort_group, table_name;

-- 3. Summary: tables, views, foreign keys, total rows
WITH expected_tables(name) AS (VALUES
    ('Roles'), ('Users'), ('AuditLogs'), ('Companies'), ('Analyses'), ('Identities'), ('IdentityScores'),
    ('PermissionUsage'), ('Findings'), ('GeneratedRoles'), ('GeneratedRoleActions'), ('RuleSettings'),
    ('HighRiskPatterns'), ('Reports'), ('Notifications'), ('Enterprises'), ('Classifications'),
    ('ClassificationTechnologies'), ('ClassificationPolicies'), ('Applications'), ('Components'),
    ('Dependencies'), ('Assessments'), ('AssessmentFactors'), ('Simulations'), ('SimulationImpacts'),
    ('Plans'), ('PlanPhases')
),
expected_views(name) AS (VALUES ('vw_UserLogin'), ('vw_LatestAssessment'), ('vw_PlanSummary'), ('vw_IdentityRisk')),
dev_objects AS (
    SELECT c.oid, c.relname, c.relkind
    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'dev'
),
counts AS (
    SELECT
        (SELECT COUNT(*) FROM dev_objects o JOIN expected_tables e ON e.name = o.relname WHERE o.relkind IN ('r', 'p')) AS table_count,
        (SELECT COUNT(*) FROM dev_objects o JOIN expected_views  e ON e.name = o.relname WHERE o.relkind = 'v')        AS view_count,
        (SELECT COUNT(*) FROM pg_constraint con
            JOIN dev_objects o ON o.oid = con.conrelid
            JOIN expected_tables e ON e.name = o.relname
         WHERE con.contype = 'f')                                                                                     AS fk_count,
        (  (SELECT COUNT(*) FROM dev."Roles") + (SELECT COUNT(*) FROM dev."Users") + (SELECT COUNT(*) FROM dev."AuditLogs")
         + (SELECT COUNT(*) FROM dev."Companies") + (SELECT COUNT(*) FROM dev."Analyses") + (SELECT COUNT(*) FROM dev."Identities")
         + (SELECT COUNT(*) FROM dev."IdentityScores") + (SELECT COUNT(*) FROM dev."PermissionUsage") + (SELECT COUNT(*) FROM dev."Findings")
         + (SELECT COUNT(*) FROM dev."GeneratedRoles") + (SELECT COUNT(*) FROM dev."GeneratedRoleActions") + (SELECT COUNT(*) FROM dev."RuleSettings")
         + (SELECT COUNT(*) FROM dev."HighRiskPatterns") + (SELECT COUNT(*) FROM dev."Reports") + (SELECT COUNT(*) FROM dev."Notifications")
         + (SELECT COUNT(*) FROM dev."Enterprises") + (SELECT COUNT(*) FROM dev."Classifications") + (SELECT COUNT(*) FROM dev."ClassificationTechnologies")
         + (SELECT COUNT(*) FROM dev."ClassificationPolicies") + (SELECT COUNT(*) FROM dev."Applications") + (SELECT COUNT(*) FROM dev."Components")
         + (SELECT COUNT(*) FROM dev."Dependencies") + (SELECT COUNT(*) FROM dev."Assessments") + (SELECT COUNT(*) FROM dev."AssessmentFactors")
         + (SELECT COUNT(*) FROM dev."Simulations") + (SELECT COUNT(*) FROM dev."SimulationImpacts") + (SELECT COUNT(*) FROM dev."Plans")
         + (SELECT COUNT(*) FROM dev."PlanPhases"))                                                                  AS total_rows
)
SELECT table_count, view_count, fk_count, total_rows,
       CASE WHEN table_count = 28 AND view_count = 4 AND fk_count = 46 THEN 'PASS' ELSE 'FAIL' END AS schema_check,
       CASE WHEN total_rows = 569 THEN 'PASS' ELSE 'FAIL' END AS seed_rows_check,
       'VERIFY RESULT: '
         || CASE WHEN table_count = 28 AND view_count = 4 AND fk_count = 46 AND total_rows = 569 THEN 'PASS' ELSE 'FAIL' END
         || format(' (tables=%s, views=%s, fks=%s, rows=%s)', table_count, view_count, fk_count, total_rows) AS result
FROM counts;

COMMIT;
