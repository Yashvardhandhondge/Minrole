# SecureShift Sprint 4 – progress log

Read this after CLAUDE.md when a session starts (or after a context reset).

- Sprint 4 was built on branch `sprint4-full` (from `main`, after PR #1) and squash-merged into `main` as PR #2.
- Owner instructions for this run: build Phases 2–6 fully, test, record videos, write docs. Don't ask questions;
  pick what is closest to the prototype + CLAUDE.md and write the choice down (section "Decisions" below).

## Status

**Sprint 4 console app complete** (2026-11-23). PR #2 squash-merged into main; branch sprint4-full deleted.

| Phase | What | Status |
|---|---|---|
| 1 | Foundation (scripts, solution, scaffold, db commands, CI) | DONE (PR #1 merged) |
| 2 | Login / logout / profile / change password, Super Admin (dashboard, users, audit logs) | DONE – CI green run 45055891860 (76 tests, e2e phase2 PASS) |
| 3 | Cloud Admin – Governance | DONE – CI green run 45057316072 (124 tests, e2e phase2+3 PASS) |
| 4 | Admin – Modernization | DONE – CI green run 45058221064 (143 tests, e2e phase2-4 PASS) |
| 5 | Architect + Executive | DONE – CI green run 45059083751 (152 tests, e2e phase2-5 + full workflow PASS) |
| 6 | Full e2e, manual run, Windows rehearsal, videos, docs | DONE |

## Phase 6 checklist

- [x] Full-workflow e2e (tests/e2e/phase6-full-workflow.sh + FullWorkflowTests) - CI green
- [x] Manual run on Docker Postgres: db setup / status / smoke, every menu of every role, live psql change (bugs 1-4 below fixed)
- [x] Windows rehearsal from a fresh clone (no Docker): setup-postgres-portable.ps1 -> run.ps1 -SetupOnly -> app (bug 5 fixed)
- [x] Docs: TCS_LAPTOP_SETUP.md, USER_GUIDE.md, DEMO_SCRIPT.md, RUNNING.md, README.md, CLAUDE.md
- [x] Videos 00-06 recorded with VHS (Windows PowerShell) and checked frame by frame: docs/demo/ + release sprint4-demo
- [x] Final CI green, PR description, cleanup (docker compose down -v, portable PostgreSQL stopped, no temporary images were pulled)

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

Owner decisions after the final report (2026-11-23):

21. Super Admin dashboard "Recent activity" hides the sign-in rows (Logged In, Logged Out, Login Failed,
    Login Blocked); the Audit Logs page still shows them.
22. Governance scoring formula kept; explained with a worked example in USER_GUIDE.md ("team to confirm").
23. Time zone: App:TimeZone = Asia/Kolkata (fallback "India Standard Time", then UTC). All times on screen and
    every date-only column (Submitted On, decision dates, report dates, "today") use the India date - at 01:00 IST
    it is already the next day (replaces decision 5).
24. Videos are only in the GitHub release sprint4-demo (docs/demo keeps the .tape files; *.mp4 / *.gif are ignored).
25. Notification preferences (profile tab of the prototype): SKIPPED by the owner - no table, no menu.
26. Offline package SecureShift-offline.zip (scripts/windows/make-offline-package.ps1): published app + scripts +
    database scripts + samples + docs; run.ps1 / setup-postgres-portable.ps1 work from it with no Git, no build and
    no internet when the PostgreSQL zip is next to run.ps1 (or in C:\SecureShiftTools).

27. The COMPLETE walkthrough video (docs/demo/full, release demo-full): every menu option of every role (287 options
    and error cases, docs/demo/full/COVERAGE.md), 29 clips recorded in order on one database, starting from the
    offline package. The clips are data (docs/demo/full/clips.js); a dry run types the same keys into the real app
    first and checks every expected text; scripts/windows/record-full-demo.ps1 records everything with one command.
28. The console wraps long lines and fits wide tables to the terminal width (text cells end with "..."), so nothing
    is cut off or wrapped mid-table on a ~120-column window. Piped output (e2e tests) is never wrapped.

## Bugs found in the manual run / rehearsal (all fixed, with a test)

1. Windows PowerShell 5.1 pipes input as UTF-8 WITH a byte-order mark: the first typed line ("1") was not recognised.
   Fix: input is read as UTF-8 and the BOM is stripped (SystemConsoleIO.StripBom, ConsoleIoTests).
2. Super Admin > Edit user: the role question was inside roles.First(...), so it was asked again for every role
   and pressing Enter could change the role (Executive -> Admin). Fix: ask once (ManualRunRegressionTests).
3. Same bug in the Analysis Wizard company question (company 2+ was asked twice). Fixed the same way.
4. A wrong number in a choice list said "Target is required"; now "Type a number from 1 to N" (the
   "required" text is only for an empty answer).
5. setup-postgres-portable.ps1 hung after "server started": the server kept the script's output pipe open.
   Fix: pg_ctl is started in its own hidden window (Start-Process + WaitForExit).
6. Docker Desktop: Host=localhost timed out (IPv6 ::1). Fix: docs and scripts use 127.0.0.1.
7. (videos) The live psql change failed: Windows PowerShell 5.1 drops embedded " quotes from native arguments.
   Fix: the SQL goes through a file (psql -f) in the tape and in DEMO_SCRIPT.md.
8. (videos) Expand-Archive drew a progress bar over the window (and is slow). Fix: System.IO.Compression.ZipFile.
9. Cosmetic: decision-history comment column wrapped; plan screens got a "Phases:" heading.
10. (full walkthrough) On a 131-column terminal long lines and wide tables wrapped in the middle of a row.
    Fix: ConsoleUi wraps text at spaces and narrows the widest text columns (ConsoleWidthTests).
11. (full walkthrough) A message with one error per line (password change with 3 field errors) was wrapped as one
    long line ("... number and" / "special character"). Fix: every line is wrapped on its own (ConsoleWidthTests).
12. (full walkthrough) A Super Admin trying to deactivate their own account got "a Super Admin cannot be deactivated";
    now the clearer "You cannot deactivate your own account" comes first (same order as the service).
13. (full walkthrough) The Architect's application list did not show the active search / assessment filter
    (the Admin's list did). It now shows "Search: ... · Assessment: ...".

## Known bugs

(none open)
