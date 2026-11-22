/* =====================================================================
   SecureShift - 04_migration_history.sql

   Creates the history table dev."__SchemaMigrations" (if it is not there yet)
   and records version 1 = "baseline" (the schema from 01 + the seed from 02).

   Used by the console:
     db setup     runs 01, 02 and then this file (brand-new empty dev schema)
     db baseline  runs only this file (faculty DB that was already set up by hand)
     db migrate   reads this table and applies migrations/V002__*.sql, V003__*.sql ...

   Safe to run again: nothing is replaced, the baseline row is added only once.
   ===================================================================== */

BEGIN;

-- Only baseline a database that really has the SecureShift tables.
DO $$
DECLARE
    t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'Roles', 'Users', 'AuditLogs', 'Companies', 'Analyses', 'Identities', 'IdentityScores',
        'PermissionUsage', 'Findings', 'GeneratedRoles', 'GeneratedRoleActions', 'RuleSettings',
        'HighRiskPatterns', 'Reports', 'Notifications', 'Enterprises', 'Classifications',
        'ClassificationTechnologies', 'ClassificationPolicies', 'Applications', 'Components',
        'Dependencies', 'Assessments', 'AssessmentFactors', 'Simulations', 'SimulationImpacts',
        'Plans', 'PlanPhases'
    ]
    LOOP
        IF to_regclass(format('dev.%I', t)) IS NULL THEN
            RAISE EXCEPTION 'Table dev.% does not exist, so this is not a SecureShift database yet. Run 01 (and 02) first.', quote_ident(t);
        END IF;
    END LOOP;
END
$$;

CREATE TABLE IF NOT EXISTS dev."__SchemaMigrations" (
    "Version"    INTEGER      NOT NULL CONSTRAINT "PK_SchemaMigrations" PRIMARY KEY,  -- 1 = baseline, 2 = V002__..., ...
    "Name"       VARCHAR(200) NOT NULL,                                               -- short_name from the file name
    "ScriptName" VARCHAR(260) NOT NULL,                                               -- V002__short_name.sql
    "Checksum"   CHAR(64)     NULL,                                                   -- SHA-256 of the file (line endings normalised)
    "AppliedAt"  TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "AppliedBy"  VARCHAR(100) NOT NULL DEFAULT CURRENT_USER
);

INSERT INTO dev."__SchemaMigrations" ("Version", "Name", "ScriptName", "Checksum")
VALUES (1, 'baseline', '01_create_secureshift_in_dev.sql + 02_seed_data_postgresql_dev.sql', NULL)
ON CONFLICT ("Version") DO NOTHING;

COMMIT;
