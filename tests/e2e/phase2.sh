#!/usr/bin/env bash
# Phase 2 end-to-end: login, forced password change, Super Admin user management, audit logs, logout.
source "$(dirname "$0")/lib.sh"
fresh_db e2e_phase2
echo "== Phase 2: Super Admin adds an Architect"
run_app p2-add 1 arya.mujumdar@secureshift.io Super@2026 \
  2 A "Esha Kulkarni" esha.kulkarni@company.com 9822001122 3 0 \
  0 y 0
expect "User Created"
expect "Share these details with the user yourself. No email is sent."
TEMP=$(grep -o 'Temp@[0-9]\{4\}x' "$LAST" | head -1)
[ -n "$TEMP" ] || { echo "no temporary password shown"; exit 1; }
expect_sql "SELECT COUNT(*) FROM dev.\"Users\" u JOIN dev.\"Roles\" r ON r.\"RoleId\" = u.\"RoleId\" WHERE u.\"Email\" = 'esha.kulkarni@company.com' AND u.\"Status\" = 'Active' AND u.\"MustChangePassword\" AND r.\"RoleCode\" = 'architect'" 1
expect_sql "SELECT \"Detail\" FROM dev.\"AuditLogs\" WHERE \"Action\" = 'User Created' AND \"Target\" = 'Esha Kulkarni'" "Role: Architect"

echo "== Phase 2: first login of the new user (forced password change)"
run_app p2-first 1 esha.kulkarni@company.com "$TEMP" "$TEMP" weak weak "$TEMP" Esha@2026x Esha@2026x 0 y 0
expect "You are using a temporary password"
expect "Min 8 characters with uppercase, lowercase, number and special character"
expect "Password updated"
expect "Logged in as Esha Kulkarni (Architect)"
expect_sql "SELECT \"MustChangePassword\" FROM dev.\"Users\" WHERE \"Email\" = 'esha.kulkarni@company.com'" f

echo "== Phase 2: wrong password, inactive user"
run_app p2-bad 1 arya.mujumdar@secureshift.io Wrong@2026 1 sakshi.patil@company.com Welcome@2026 0
expect "Invalid email or password."
expect "Your account is inactive. Please contact your Super Admin."

echo "== Phase 2: deactivate, reset password, Super Admin rule"
# open page 3 (users 21-25 + Esha), row 6 = Esha (USR-026); deactivate; reset password; then Rohan (Super Admin) cannot be deactivated
run_app p2-manage 1 arya.mujumdar@secureshift.io Super@2026 \
  2 S esha 1 4 y "Left project" 3 y 0 S rohan 1 4 0 0 \
  0 y 0
expect "Esha Kulkarni deactivated"
expect "Password Reset"
expect "A Super Admin cannot be deactivated or removed"
expect_sql "SELECT \"Status\" FROM dev.\"Users\" WHERE \"Email\" = 'esha.kulkarni@company.com'" Inactive
expect_sql "SELECT COUNT(*) FROM dev.\"AuditLogs\" WHERE \"Action\" = 'Password Reset' AND \"Target\" = 'Esha Kulkarni' AND \"Detail\" = 'Temporary password given manually'" 1

echo "== Phase 2: audit logs filter + profile"
run_app p2-audit 1 arya.mujumdar@secureshift.io Super@2026 3 A "Account Deactivated" 0 4 0 0 y 0
expect "Action: Account Deactivated"
expect "Esha Kulkarni"
expect "Profile & Settings"
expect_sql "SELECT COUNT(*) FROM dev.\"AuditLogs\" WHERE \"Action\" = 'Logged Out'" 4
echo "Phase 2 e2e: PASS"
