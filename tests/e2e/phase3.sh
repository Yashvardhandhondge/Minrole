#!/usr/bin/env bash
# Phase 3 end-to-end: Cloud Admin (Governance) - Analysis Wizard with typed JSON paths, bad file, results, rules, report.
source "$(dirname "$0")/lib.sh"
fresh_db e2e_phase3
S=samples/governance
LOGIN=(1 aarav.mehta@secureshift.io Cloud@2026)

echo "== Phase 3: Analysis Wizard - bad file first, then the 3 sample files"
run_app p3-wizard "${LOGIN[@]}" \
  3 1 4 08/12/2026 11/09/2026 "$S/bad/role-assignments-missing-fields.json" "$S/activity-log-90d.json" "$S/roles-catalog.json" \
  3 1 4 08/12/2026 11/09/2026 "$S/role-assignments.json" "$S/activity-log-90d.json" "$S/roles-catalog.json" y \
  0 y 0
expect "Missing fields check - role-assignments-missing-fields.json record 2 has no principalId, scope"
expect "Nothing was saved."
expect "Schema validation - All 3 files match the expected JSON structure"
expect "Activity coverage - Activity Log covers 08/12/2026 - 11/09/2026 (90 days) for Northwind Traders"
expect "Analysis Wizard - Step 4 of 4: Results"
CODE=$(sql "SELECT \"Code\" FROM dev.\"Analyses\" ORDER BY \"AnalysisId\" DESC LIMIT 1")
expect "$CODE saved"
expect_sql "SELECT COUNT(*) FROM dev.\"Analyses\"" 5
expect_sql "SELECT \"IdentityCount\" || '/' || \"PeriodDays\" || '/' || \"Status\" FROM dev.\"Analyses\" WHERE \"Code\" = '$CODE'" "20/90/Complete"
expect_sql "SELECT s.\"RiskScore\" FROM dev.\"IdentityScores\" s JOIN dev.\"Analyses\" a ON a.\"AnalysisId\" = s.\"AnalysisId\" JOIN dev.\"Identities\" i ON i.\"IdentityId\" = s.\"IdentityId\" WHERE a.\"Code\" = '$CODE' AND i.\"UserName\" = 'svc-webapp-deploy'" 91
expect_sql "SELECT COUNT(*) = MAX(a.\"HighRiskCount\") FROM dev.\"Findings\" f JOIN dev.\"Analyses\" a ON a.\"AnalysisId\" = f.\"AnalysisId\" WHERE a.\"Code\" = '$CODE'" t
expect_sql "SELECT COUNT(*) FROM dev.\"GeneratedRoleActions\" x JOIN dev.\"GeneratedRoles\" r ON r.\"GeneratedRoleId\" = x.\"GeneratedRoleId\" JOIN dev.\"Analyses\" a ON a.\"AnalysisId\" = r.\"AnalysisId\" WHERE a.\"Code\" = '$CODE' AND x.\"Note\" = 'used once, 85 days ago'" 1
expect_sql "SELECT COUNT(*) FROM dev.\"Notifications\" WHERE \"Message\" = 'Analysis $CODE completed for Northwind Traders'" 1

echo "== Phase 3: identities, findings, roles, rules, patterns, report"
run_app p3-screens "${LOGIN[@]}" \
  1 5 1 0 0 6 V HIGH 0 7 1 1 0 0 \
  8 1 50 30 30 "90 days" 3 Yes 1 40 40 20 "60 days" 4 No 0 \
  9 A "Microsoft.Sql/*/write" 0 \
  10 G "Northwind Traders" 0 \
  11 y \
  0 y 0
expect "Latest analysis complete - Northwind Traders"
expect "Why this score"
expect "Severity: HIGH"
expect "marked as reviewed"
expect "Weights must add up to 100% before saving"
expect "rules.json saved"
expect "Pattern added"
expect "generated for Northwind Traders"
expect_sql "SELECT \"WeightUnused\" || '/' || \"WeightHighRisk\" || '/' || \"WeightScope\" || '/' || \"DefaultPeriodDays\" FROM dev.\"RuleSettings\" WHERE \"IsActive\"" "40/40/20/60"
expect_sql "SELECT COUNT(*) FROM dev.\"Reports\" r JOIN dev.\"Analyses\" a ON a.\"AnalysisId\" = r.\"AnalysisId\" WHERE a.\"Code\" = '$CODE'" 1
expect_sql "SELECT COUNT(*) FROM dev.\"Notifications\" WHERE NOT \"IsRead\" AND \"UserId\" = 25" 0
echo "Phase 3 e2e: PASS"
