#!/usr/bin/env bash
# Phase 4 end-to-end: Admin (Modernization) - register an enterprise, classify it (EMSI), application, component, link, twin.
source "$(dirname "$0")/lib.sh"
fresh_db e2e_phase4
LOGIN=(1 sachin.kumar@secureshift.io Admin@2026)

echo "== Phase 4: register + classify + application + component + dependency + twin"
run_app p4-admin "${LOGIN[@]}" \
  2 "Tailspin Airlines" 8 Pune "Kavita Desai" kavita.desai@tailspin.com 9812300011 \
  5 12 20 8 250000 3 400 2 1 2 30 1,3 2,4 3 4 3 3 y 12 20 8 250000 3 400 2 1 2 30 "" "" 3 4 3 2 \
  6 R "Flight Booking" Sales "Web Team" 1 3 1 "Java 17" 1 6 1 2 300000 3 1 1 1 1 0 \
  7 A 1 1 "Booking Database" L 1 2 4 "Stores bookings" L 1 2 4 "" n I 0 \
  8 0 \
  4 0 \
  0 y 0
expect "Tailspin Airlines registered as ENT-004"
expect "Low + Medium + High + Critical must equal Total applications"
expect "[OK] EMSI 32.33 - Growth category"
expect "Growth Subscription Plan: Monthly ₹10,000"
expect "Flight Booking registered as APP-011"
expect "Service Booking Database added (N-20)"
expect "Dependency DEP-021 saved"
expect "Target: This link already exists"
expect "[APP-011] Flight Booking  (Application · High · Dependency Impact 45/100)"
expect "depends on → Booking Database (Service)  [Critical]  DEP-021"
expect_sql "SELECT \"EmsiScore\" || ' ' || \"Category\" || ' ' || \"CloudType\" FROM dev.\"Classifications\" WHERE \"EnterpriseId\" = 4" "32.33 Growth Public Cloud"
expect_sql "SELECT string_agg(\"Technology\", ',' ORDER BY \"Technology\" COLLATE \"C\") FROM dev.\"ClassificationTechnologies\" t JOIN dev.\"Classifications\" c ON c.\"ClassificationId\" = t.\"ClassificationId\" WHERE c.\"EnterpriseId\" = 4" ".NET,Java"
expect_sql "SELECT \"Code\" || ' ' || \"Name\" || ' ' || \"AgeYears\" FROM dev.\"Applications\" WHERE \"EnterpriseId\" = 4" "APP-011 Flight Booking 6"
expect_sql "SELECT COUNT(*) FROM dev.\"Dependencies\" WHERE \"EnterpriseId\" = 4 AND \"Criticality\" = 'Critical'" 1
expect_sql "SELECT string_agg(\"Action\", ',' ORDER BY \"AuditLogId\") FROM dev.\"AuditLogs\" WHERE \"UserId\" = 12 AND \"AuditLogId\" > 8 AND \"Action\" NOT IN ('Logged In','Logged Out')" "Enterprise Registered,Enterprise Classified,Application Registered,Component Added,Dependency Added"

echo "== Phase 4: delete rules"
run_app p4-delete "${LOGIN[@]}" 6 1 2 y 0 3 2 6 4 2 y 0 0 y 0
expect "has assessments or simulations, so it cannot be deleted"
expect "Supplier Portal deleted"
expect_sql "SELECT COUNT(*) FROM dev.\"Applications\" WHERE \"Name\" = 'Supplier Portal'" 0
echo "Phase 4 e2e: PASS"
