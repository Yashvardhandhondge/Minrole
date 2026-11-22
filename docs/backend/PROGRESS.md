# SecureShift Sprint 4 – progress log

Read this after CLAUDE.md when a session starts (or after a context reset).

- Branch: `sprint4-full` (from `main`, after PR #1 was merged). One PR to main, ready for review, NOT merged.
- Owner instructions for this run: build Phases 2–6 fully, test, record videos, write docs. Don't ask questions;
  pick what is closest to the prototype + CLAUDE.md and write the choice down (section "Decisions" below).

## Status

| Phase | What | Status |
|---|---|---|
| 1 | Foundation (scripts, solution, scaffold, db commands, CI) | DONE (PR #1 merged) |
| 2 | Login / logout / profile / change password, Super Admin (dashboard, users, audit logs) | DONE – CI green run 45055891860 (76 tests, e2e phase2 PASS) |
| 3 | Cloud Admin – Governance | DONE – CI green run 45057316072 (124 tests, e2e phase2+3 PASS) |
| 4 | Admin – Modernization | DONE – CI green run 45058221064 (143 tests, e2e phase2-4 PASS) |
| 5 | Architect + Executive | DONE – CI green run 45059083751 (152 tests, e2e phase2-5 + full workflow PASS) |
| 6 | Full e2e, manual run, Windows rehearsal, videos, docs | in progress (full workflow e2e DONE) |

## Next step

Phase 6: Windows rehearsal from a fresh clone (scripts/windows/*.ps1), manual run of every menu on Docker
Postgres + live psql change, VHS videos (docs/demo/*.tape), docs (TCS_LAPTOP_SETUP.md, USER_GUIDE.md,
DEMO_SCRIPT.md, RUNNING.md, README.md), final PR text, cleanup (docker compose down -v, stop portable PG).

## Environment notes (this laptop)

- RAM: keep everything < 4 GB (another agent shares the laptop). `dotnet build -m:1`, `DOTNET_gcServer=0`.
- Docker Desktop is running (the owner switched it on). Local dev DB: `docker compose -f docker-compose.dev.yml up -d`
  (container secureshift-pg, 512 MB limit, port 5433, use Host=127.0.0.1). Databases: `secureshift` (manual play),
  `secureshift_ci` (pristine seed for the read-only tests). Portable PostgreSQL zip is in C:\SecureShiftTools (for the rehearsal).
- Tools allowed without asking: VHS, ttyd, ffmpeg, portable PostgreSQL 16 → all in `C:\SecureShiftTools\`.

## Decisions (prototype vs DB, and other choices)

See the list at the end of this file; it is copied into the final report.

1. Super Admin user management shows the **Modernization** users only (the 4 roles in the prototype's
   `ROLES` list). The Cloud Admin account is not listed there, so the dashboard numbers match the prototype
   (24 users, 20 active, 4 inactive).
2. A Super Admin can never be deactivated or have the role changed (prototype rule). This also covers
   "cannot deactivate yourself" and "keep at least one active Super Admin".
3. No lockout (the prototype has none).
4. Login also writes "Login Failed" (known email, wrong password) and "Login Blocked" (inactive) audit rows;
   the message on screen stays the generic "Invalid email or password." for unknown email and wrong password.
5. Times are stored in UTC and shown in the local time of the machine (seed times therefore show +5:30 in India).
6. Testing: tests that write data get their own fresh database (CREATE DATABASE + db setup); the shared seeded
   database stays untouched for the read-only checks (569 rows).
7. Docker Desktop on Windows: use Host=127.0.0.1, not localhost (localhost goes to IPv6 ::1 and times out).
8. Governance scoring: the prototype has no formula (it only shows 3 signals with weights 50/30/20). SecureShift:
   ratio = Math.round((1-used/granted)*100); high-risk = min(100, 10 x unused high-risk actions);
   scope = Subscription/MG 100, RG 40, Resource 10; score = Math.round(weighted sum / 100). Gives 91 for svc-webapp-deploy.
9. Findings: only the 4 DB categories (Delete, Network, Key Vault, Role Assignment); severity HIGH for Subscription /
   Management Group scope or Role Assignment, MEDIUM for Resource Group, LOW for Resource.
10. Generated roles only for identities that used something but not everything; actions used fewer than RareThreshold
    times are "rare" (note "used once, 85 days ago" counted from the period end) and are not in the role JSON.
11. Analysis code = "AN-<year>-" + (1111 + number of analyses) like analysis.js (next free number if taken);
    report code = RPT-(highest number + 1) like reports.js.
12. Rule Settings are saved as a NEW active row (old row IsActive = false) so the history is kept.
13. Analysis Wizard: the "Last N days" presets end yesterday (prototype); the sample files cover 08/12/2026-11/09/2026,
    so demos use "Custom range". Companies without an analysis show "Not analysed" / PENDING (no invented numbers).
14. Identity evidence rows (SS.GOV.evidence) are fake in the prototype and not stored in the DB: not shown.
15. Classification limits follow the DB where it is stricter: Total applications 1-10,000 (prototype 1-100,000).
    Cloud types are the prototype's 4; "Multi-Cloud" is stored as "Multi Cloud" (DB spelling); On-Premise scores 0.
16. Applications: age 0-50 (DB; prototype 0-60); status Active / Legacy (prototype list); description of a link max 200 (DB; prototype 500).
17. An application with assessments or simulations cannot be deleted (the DB keeps the history; the prototype deletes it).
18. Simulation uses the TD, GR AND DI of the latest assessment (the DB stores DI with the assessment); the prototype
    recalculates DI at simulation time. Same numbers unless the dependencies changed after the assessment (re-assess).
    Raw dependency points of a stored simulation are not stored, so the plan snapshot shows DI only.
19. Plan decisions lock the row (SELECT ... FOR UPDATE): two Executives cannot decide the same plan.
20. An approval without a comment stores "Approved." (review-plan.js does the same).

## Known bugs

(none yet)
