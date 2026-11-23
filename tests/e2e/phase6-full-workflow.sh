#!/usr/bin/env bash
# FULL WORKFLOW (all roles, one database, one run), checking the database after every step:
#   1. Super Admin adds an Architect
#   2. Admin registers + classifies an enterprise, adds an application and a dependency
#   3. the NEW Architect logs in (forced password change), assesses, simulates, creates and submits a plan
#   4. Executive approves
#   5. Cloud Admin runs an analysis from the sample JSON files
source "$(dirname "$0")/lib.sh"
fresh_db e2e_full

echo "== 1. Super Admin adds an Architect"
run_app full-1 1 arya.mujumdar@secureshift.io Super@2026 2 A "Nisha Verma" nisha.verma@company.com 9811223344 3 0 0 y 0
expect "User Created"
TEMP=$(grep -o 'Temp@[0-9]\{4\}x' "$LAST" | head -1)
[ -n "$TEMP" ] || { echo "no temporary password shown"; exit 1; }
expect_sql "SELECT r.\"RoleCode\" || '/' || u.\"Status\" || '/' || u.\"MustChangePassword\" FROM dev.\"Users\" u JOIN dev.\"Roles\" r ON r.\"RoleId\" = u.\"RoleId\" WHERE u.\"Email\" = 'nisha.verma@company.com'" "architect/Active/true"

echo "== 2. Admin registers an enterprise, classifies it, adds an application and a dependency"
run_app full-2 1 sachin.kumar@secureshift.io Admin@2026 \
  2 "Tailspin Airlines" 8 Pune "Kavita Desai" kavita.desai@tailspin.com 9812300011 \
  5 12 20 8 250000 3 400 2 1 2 30 1,3 2,4 3 4 3 2 \
  6 R "Flight Booking" Sales "Web Team" 1 3 1 "Java 17" 1 6 1 2 300000 4 1 1 1 1 0 \
  7 A 2 1 "Booking Database" L 1 2 4 "Stores bookings" 0 \
  0 y 0
expect "Tailspin Airlines registered as ENT-004"
expect "[OK] EMSI 32.33 - Growth category"
expect "Flight Booking registered as APP-011"
expect "Dependency DEP-021 saved"
expect_sql "SELECT \"EmsiScore\" || '/' || \"Category\" FROM dev.\"Classifications\" WHERE \"EnterpriseId\" = 4" "32.33/Growth"
expect_sql "SELECT COUNT(*) FROM dev.\"Applications\" a JOIN dev.\"Dependencies\" d ON d.\"SourceApplicationId\" = a.\"ApplicationId\" WHERE a.\"Code\" = 'APP-011' AND d.\"Criticality\" = 'Critical'" 1

echo "== 3. The new Architect: first login, assess Flight Booking, simulate Refactor, create + submit the plan"
run_app full-3 1 nisha.verma@company.com "$TEMP" "$TEMP" Nisha@2026x Nisha@2026x \
  2 4 4 11 1 2 3 1 "" "First Tailspin plan" 2 y \
  0 y 0
expect "Password updated"
expect "Logged in as Nisha Verma (Architect)"
expect "Assessment ASM-006 saved"
expect "Simulation SIM-007 complete"
expect "PLAN-007 submitted for approval"
expect_sql "SELECT s.\"Strategy\" || '/' || p.\"Status\" || '/' || p.\"PlanName\" FROM dev.\"Plans\" p JOIN dev.\"Simulations\" s ON s.\"SimulationId\" = p.\"SimulationId\" WHERE p.\"Code\" = 'PLAN-007'" "Refactor/Submitted/Flight Booking Refactor Plan"
expect_sql "SELECT SUM(ph.\"Weeks\") = MAX(s.\"EffortWeeks\") FROM dev.\"PlanPhases\" ph JOIN dev.\"Plans\" p ON p.\"PlanId\" = ph.\"PlanId\" JOIN dev.\"Simulations\" s ON s.\"SimulationId\" = p.\"SimulationId\" WHERE p.\"Code\" = 'PLAN-007'" t
expect_sql "SELECT u.\"FullName\" FROM dev.\"Plans\" p JOIN dev.\"Users\" u ON u.\"UserId\" = p.\"CreatedBy\" WHERE p.\"Code\" = 'PLAN-007'" "Nisha Verma"

echo "== 4. Executive approves"
run_app full-4 1 shivanand.kumatekar@secureshift.io Exec@2026 1 1 1 "Approved for Tailspin" y 0 0 y 0
expect "PLAN-007 approved"
expect_sql "SELECT \"Status\" || '/' || \"ExecutiveComment\" FROM dev.\"Plans\" WHERE \"Code\" = 'PLAN-007'" "Approved/Approved for Tailspin"

echo "== 5. Cloud Admin runs an analysis from the sample JSON files"
S=samples/governance
run_app full-5 1 aarav.mehta@secureshift.io Cloud@2026 3 1 4 08/12/2026 11/09/2026 "$S/role-assignments.json" "$S/activity-log-90d.json" "$S/roles-catalog.json" y 0 y 0
expect "Analysis Wizard - Step 4 of 4: Results"
expect_sql "SELECT \"Code\" || '/' || \"IdentityCount\" || '/' || \"Status\" FROM dev.\"Analyses\" ORDER BY \"AnalysisId\" DESC LIMIT 1" "AN-2026-1115/20/Complete"

echo "== Audit trail of the whole workflow"
expect_sql "SELECT string_agg(\"Action\", ',' ORDER BY \"AuditLogId\") FROM dev.\"AuditLogs\" WHERE \"AuditLogId\" > 8 AND \"Action\" NOT IN ('Logged In','Logged Out')" \
  "User Created,Enterprise Registered,Enterprise Classified,Application Registered,Component Added,Dependency Added,Password Changed,Assessment Run,Simulation Run,Plan Submitted,Plan Approved,Analysis Run"
echo "Full workflow e2e: PASS"
