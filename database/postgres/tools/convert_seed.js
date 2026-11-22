#!/usr/bin/env node
/* =====================================================================
   SecureShift - convert_seed.js   (Node 18+, no npm packages needed)

   ONE-TIME converter that created 02_seed_data_postgresql_dev.sql from the
   SQL Server seed database/02_seed_data.sql. Kept so anyone can see (and
   repeat) exactly how the Postgres seed was made:

     - every row is copied with the same values and the same IDs
       (rows that had no explicit ID in SQL Server get the ID the
        IDENTITY column gave them: 1, 2, 3 ... in insert order)
     - BIT 1/0                     -> TRUE/FALSE
     - DATEADD(DAY, -n, SYSUTCDATETIME()) -> CURRENT_TIMESTAMP - INTERVAL 'n days'
     - auth.X / gov.X / emdt.X     -> dev."X"  (quoted PascalCase)
     - PasswordHash: the old unsalted SHA-256 hex is checked against the known
       password, then replaced by an ASP.NET Core Identity V3 hash
       (see hash_passwords.js). Re-running makes NEW random salts: the hashes
       change, the passwords do not.

   Usage (from the repo root):
     node database/postgres/tools/convert_seed.js
   ===================================================================== */
'use strict';
const fs = require('fs');
const path = require('path');
const { hashPassword, legacySha256Hex, expectedPasswordFor } = require('./hash_passwords');

const repoRoot = path.join(__dirname, '..', '..', '..');
const SRC = path.join(repoRoot, 'database', '02_seed_data.sql');
const OUT = path.join(repoRoot, 'database', 'postgres', '02_seed_data_postgresql_dev.sql');

// Table -> identity column (null = no identity, composite key) and BIT columns.
const TABLES = {
  Roles:                      { id: 'RoleId' },
  Users:                      { id: 'UserId', bools: ['MustChangePassword'] },
  AuditLogs:                  { id: 'AuditLogId' },
  Companies:                  { id: 'CompanyId' },
  Analyses:                   { id: 'AnalysisId' },
  Identities:                 { id: 'IdentityId' },
  IdentityScores:             { id: 'IdentityScoreId' },
  PermissionUsage:            { id: 'PermissionUsageId' },
  Findings:                   { id: 'FindingId' },
  GeneratedRoles:             { id: 'GeneratedRoleId', bools: ['IncludeBaselineRead', 'IsReviewed'] },
  GeneratedRoleActions:       { id: 'GeneratedRoleActionId', bools: ['IsRare'] },
  RuleSettings:               { id: 'RuleSettingId', bools: ['IncludeBaselineRead', 'IsActive'] },
  HighRiskPatterns:           { id: 'PatternId', bools: ['IsEnabled'] },
  Reports:                    { id: 'ReportId' },
  Notifications:              { id: 'NotificationId', bools: ['IsRead'] },
  Enterprises:                { id: 'EnterpriseId' },
  Classifications:            { id: 'ClassificationId' },
  ClassificationTechnologies: { id: null },
  ClassificationPolicies:     { id: null },
  Applications:               { id: 'ApplicationId', bools: ['HasSensitiveData'] },
  Components:                 { id: 'ComponentId' },
  Dependencies:               { id: 'DependencyId' },
  Assessments:                { id: 'AssessmentId' },
  AssessmentFactors:          { id: 'AssessmentFactorId' },
  Simulations:                { id: 'SimulationId' },
  SimulationImpacts:          { id: 'SimulationImpactId' },
  Plans:                      { id: 'PlanId' },
  PlanPhases:                 { id: 'PlanPhaseId' },
};

/* ---------- tiny SQL tokenizer for "INSERT ... VALUES (...), (...);" ---------- */

// Returns the list of top-level "( ... )" groups up to the terminating ';'.
function readRows(sql, start) {
  const rows = [];
  let i = start, depth = 0, inStr = false, cur = '';
  for (; i < sql.length; i++) {
    const ch = sql[i];
    if (inStr) {
      cur += ch;
      if (ch === "'") {
        if (sql[i + 1] === "'") { cur += "'"; i++; } else inStr = false;
      }
      continue;
    }
    if (ch === "'") { inStr = true; cur += ch; continue; }
    if (ch === '(') { if (depth > 0) cur += ch; depth++; continue; }
    if (ch === ')') { depth--; if (depth > 0) cur += ch; else { rows.push(cur); cur = ''; } continue; }
    if (depth === 0 && ch === ';') return { rows, end: i + 1 };
    if (depth > 0) cur += ch;
  }
  throw new Error('INSERT without terminating ;');
}

// Splits one row "1, 'a, b', NULL, f(x, y)" into values, respecting quotes and parentheses.
function splitValues(row) {
  const vals = [];
  let depth = 0, inStr = false, cur = '';
  for (let i = 0; i < row.length; i++) {
    const ch = row[i];
    if (inStr) {
      cur += ch;
      if (ch === "'") { if (row[i + 1] === "'") { cur += "'"; i++; } else inStr = false; }
      continue;
    }
    if (ch === "'") { inStr = true; cur += ch; continue; }
    if (ch === '(') depth++;
    if (ch === ')') depth--;
    if (ch === ',' && depth === 0) { vals.push(cur.trim()); cur = ''; continue; }
    cur += ch;
  }
  vals.push(cur.trim());
  return vals;
}

function unquote(v) {
  if (!/^'.*'$/s.test(v)) throw new Error('not a string literal: ' + v);
  return v.slice(1, -1).replace(/''/g, "'");
}

function convertValue(table, col, v) {
  if (v === 'NULL') return v;
  if ((TABLES[table].bools || []).includes(col)) {
    if (v === '1') return 'TRUE';
    if (v === '0') return 'FALSE';
    throw new Error(`${table}.${col}: unexpected BIT value ${v}`);
  }
  const m = /^DATEADD\(DAY,\s*-(\d+),\s*SYSUTCDATETIME\(\)\)$/i.exec(v);
  if (m) return `CURRENT_TIMESTAMP - INTERVAL '${m[1]} days'`;
  const isString = /^'.*'$/s.test(v);
  if (/^N'/.test(v) || (!isString && !/^-?\d+(\.\d+)?$/.test(v))) throw new Error(`${table}.${col}: unconverted T-SQL: ${v}`);
  return v;
}

/* ---------- main ---------- */

const src = fs.readFileSync(SRC, 'utf8');
const insertRe = /INSERT INTO (auth|gov|emdt)\.(\w+) \(([^)]*)\) VALUES/g;
const blocks = [];
let m;
while ((m = insertRe.exec(src)) !== null) {
  const [, , table, colList] = m;
  if (!TABLES[table]) throw new Error('unknown table ' + table);
  const cols = colList.split(',').map(s => s.trim());
  const { rows, end } = readRows(src, insertRe.lastIndex);
  insertRe.lastIndex = end;
  blocks.push({ table, cols, rows: rows.map(splitValues) });
}

let total = 0;
const out = [];
const counts = [];
for (const b of blocks) {
  const meta = TABLES[b.table];
  let cols = b.cols.slice();
  let rows = b.rows;
  rows.forEach((r, i) => { if (r.length !== cols.length) throw new Error(`${b.table} row ${i + 1}: ${r.length} values for ${cols.length} columns`); });

  // Give rows without an explicit ID the ID SQL Server's IDENTITY gave them (1..N).
  if (meta.id && !cols.includes(meta.id)) {
    cols = [meta.id, ...cols];
    rows = rows.map((r, i) => [String(i + 1), ...r]);
  }

  // Password hashes: check the legacy SHA-256 matches the expected password, then make a V3 hash.
  if (b.table === 'Users') {
    const iEmail = cols.indexOf('Email'), iHash = cols.indexOf('PasswordHash');
    rows = rows.map(r => {
      const email = unquote(r[iEmail]);
      const pw = expectedPasswordFor(email);
      const legacy = unquote(r[iHash]);
      if (legacy !== legacySha256Hex(pw)) throw new Error(`legacy hash for ${email} is not SHA-256('${pw}')`);
      const copy = r.slice();
      copy[iHash] = `'${hashPassword(pw)}'`;
      return copy;
    });
  }

  const lines = rows.map(r => '  (' + r.map((v, i) => convertValue(b.table, cols[i], v)).join(', ') + ')');
  out.push(`-- dev."${b.table}": ${rows.length} rows`);
  out.push(`INSERT INTO dev."${b.table}" (${cols.map(c => `"${c}"`).join(', ')}) VALUES`);
  out.push(lines.join(',\n') + ';');
  out.push('');
  counts.push([b.table, rows.length]);
  total += rows.length;
}

if (blocks.length !== 28) throw new Error(`expected 28 tables, found ${blocks.length}`);
if (total !== 569) throw new Error(`expected 569 rows, found ${total}`);

const tableNames = Object.keys(TABLES);
function chunk(list, size) { const r = []; for (let i = 0; i < list.length; i += size) r.push(list.slice(i, i + size)); return r; }
const header = `/* =====================================================================
   SecureShift - 02_seed_data_postgresql_dev.sql
   PostgreSQL version of database/02_seed_data.sql: the same ${total} rows with
   the same IDs, loaded into the 28 tables of schema dev.
   Generated by database/postgres/tools/convert_seed.js (do not hand-edit rows).

   Safe by design:
     - ONE transaction: all rows are loaded or none.
     - The DO block below STOPS with an error unless all 28 tables exist and
       are EMPTY, so it can never add duplicates to a database that has data.
     - At the end every identity sequence is moved past the highest ID, so the
       next INSERT from the app gets the next free number.

   Login accounts (PasswordHash = ASP.NET Core Identity V3: PBKDF2-HMACSHA512,
   100000 iterations, 16-byte salt, 32-byte subkey, base64):
     Cloud Admin  aarav.mehta@secureshift.io          Cloud@2026
     Super Admin  arya.mujumdar@secureshift.io        Super@2026
     Admin        sachin.kumar@secureshift.io         Admin@2026
     Architect    amanjeet.sharma@secureshift.io      Architect@2026
     Executive    shivanand.kumatekar@secureshift.io  Exec@2026
   All other users: Welcome@2026 (MustChangePassword = true).
   Check the hashes:  node database/postgres/tools/hash_passwords.js check-seed

   How to run (psql):
     psql "<connection>" -v ON_ERROR_STOP=1 -f 02_seed_data_postgresql_dev.sql
   Or with the console:  dotnet run --project src/SecureShift.Console -- db seed
   ===================================================================== */

BEGIN;

-- The seed dates/times are UTC (SQL Server used SYSUTCDATETIME). Only for this transaction.
SET LOCAL TIME ZONE 'UTC';

-- Stop unless all 28 tables exist and are empty.
DO $$
DECLARE
    t text;
    has_rows boolean;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        ${chunk(tableNames.map(t => `'${t}'`), 4).map(c => c.join(', ')).join(',\n        ')}
    ]
    LOOP
        IF to_regclass(format('dev.%I', t)) IS NULL THEN
            RAISE EXCEPTION 'Table dev.% does not exist. Run 01_create_secureshift_in_dev.sql first.', quote_ident(t);
        END IF;
        EXECUTE format('SELECT EXISTS (SELECT 1 FROM dev.%I)', t) INTO has_rows;
        IF has_rows THEN
            RAISE EXCEPTION 'Table dev.% already has rows. The seed only runs on EMPTY tables (nothing was changed).', quote_ident(t);
        END IF;
    END LOOP;
END
$$;

`;

const setvals = tableNames.filter(t => TABLES[t].id).map(t => {
  const id = TABLES[t].id;
  return `SELECT setval(pg_get_serial_sequence('dev."${t}"', '${id}'), COALESCE((SELECT MAX("${id}") FROM dev."${t}"), 0) + 1, false);`;
});

const footer = `-- Move every identity sequence past the highest ID (next INSERT gets MAX + 1).
${setvals.join('\n')}

COMMIT;

-- Done: ${total} rows loaded. Check with 03_verify_dev.sql
`;

fs.writeFileSync(OUT, header + out.join('\n') + '\n' + footer, 'utf8');
console.log(counts.map(([t, n]) => `${t.padEnd(28)} ${n}`).join('\n'));
console.log(`${'TOTAL'.padEnd(28)} ${total}\nWrote ${path.relative(repoRoot, OUT)}`);
