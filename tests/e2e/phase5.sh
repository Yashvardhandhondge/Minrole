#!/usr/bin/env bash
# Phase 5 end-to-end: Architect (assess, simulate, plan, submit, draft) + Executive (approve, reject with reason).
source "$(dirname "$0")/lib.sh"
fresh_db e2e_phase5

echo "== Phase 5: Architect assesses Customer360, simulates Replatform, submits the plan"
run_app p5-architect 1 amanjeet.sharma@secureshift.io Architect@2026 \
  1 4 2 1 2 2 1 "" "Lift to managed .NET" 2 y \
  7 T Draft 1 1 "Payment Gateway Refactor Option" "" 0 \
  0 y 0
expect "Assessment ASM-006 saved: TD 47, GR 72"
expect "Simulation SIM-007 complete: Medium risk"
expect "Estimated Cost     : ₹14.11 lakh"
expect "Total estimated effort: 16 = 16 weeks"
expect "PLAN-007 submitted for approval"
expect "PLAN-006 saved as draft"
expect_sql "SELECT \"TechnicalDebt\" || '/' || \"GovernanceRisk\" || '/' || \"DependencyImpact\" FROM dev.\"Assessments\" WHERE \"Code\" = 'ASM-006'" "47/72/59"
expect_sql "SELECT COUNT(*) FROM dev.\"AssessmentFactors\" WHERE \"AssessmentId\" = 6" 11
expect_sql "SELECT \"ModernizationRisk\" || '/' || \"EffortWeeks\" || '/' || \"CostLakh\" || '/' || \"RiskLevel\" FROM dev.\"Simulations\" WHERE \"Code\" = 'SIM-007'" "57/16/14.11/Medium"
expect_sql "SELECT string_agg(\"AffectedName\", ' > ' ORDER BY \"SequenceNo\") FROM dev.\"SimulationImpacts\" WHERE \"SimulationId\" = 7" "Customer Database > Banking API"
expect_sql "SELECT \"Status\" || '/' || \"PlanName\" FROM dev.\"Plans\" WHERE \"Code\" = 'PLAN-007'" "Submitted/Customer360 Replatform Plan"
expect_sql "SELECT string_agg(\"Weeks\"::text, ',' ORDER BY \"PhaseNo\") FROM dev.\"PlanPhases\" WHERE \"PlanId\" = 7" "2,3,7,4"
expect_sql "SELECT \"PlanName\" || '/' || \"Status\" FROM dev.\"Plans\" WHERE \"Code\" = 'PLAN-006'" "Payment Gateway Refactor Option/Draft"

echo "== Phase 5: Executive approves PLAN-007 and rejects PLAN-001 (a reason is required)"
run_app p5-executive 1 shivanand.kumatekar@secureshift.io Exec@2026 \
  1 1 1 "Go ahead in Q1" y 1 2 no "Too costly this year" y 0 \
  3 \
  0 y 0
expect "Plans awaiting review (4)"
expect "PLAN-007 approved"
expect "Please give a reason (at least 5 characters) to reject"
expect "PLAN-001 rejected"
expect "Decision history"
expect_sql "SELECT \"Status\" || '/' || \"ExecutiveComment\" || '/' || \"DecidedBy\" FROM dev.\"Plans\" WHERE \"Code\" = 'PLAN-007'" "Approved/Go ahead in Q1/14"
expect_sql "SELECT \"Status\" || '/' || \"ExecutiveComment\" FROM dev.\"Plans\" WHERE \"Code\" = 'PLAN-001'" "Rejected/Too costly this year"
expect_sql "SELECT COUNT(*) FROM dev.\"AuditLogs\" WHERE \"Action\" IN ('Plan Approved','Plan Rejected') AND \"UserId\" = 14 AND \"AuditLogId\" > 8" 2
echo "Phase 5 e2e: PASS"
