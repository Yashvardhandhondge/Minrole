# SecureShift Sprint 4 – progress log

Read this after CLAUDE.md when a session starts (or after a context reset).

- Branch: `sprint4-full` (from `main`, after PR #1 was merged). One PR to main, ready for review, NOT merged.
- Owner instructions for this run: build Phases 2–6 fully, test, record videos, write docs. Don't ask questions;
  pick what is closest to the prototype + CLAUDE.md and write the choice down (section "Decisions" below).

## Status

| Phase | What | Status |
|---|---|---|
| 1 | Foundation (scripts, solution, scaffold, db commands, CI) | DONE (PR #1 merged) |
| 2 | Login / logout / profile / change password, Super Admin (dashboard, users, audit logs) | in progress |
| 3 | Cloud Admin – Governance | todo |
| 4 | Admin – Modernization | todo |
| 5 | Architect + Executive | todo |
| 6 | Full e2e, manual run, Windows rehearsal, videos, docs | todo |

## Next step

Phase 2: IConsoleIO + Ui helpers, AuthService (V3 + legacy SHA-256 upgrade), session re-check, profile,
Super Admin menus, tests (service + scripted menu + CI e2e pipe).

## Environment notes (this laptop)

- RAM: keep everything < 4 GB (another agent shares the laptop). `dotnet build -m:1`, `DOTNET_gcServer=0`.
- Docker Desktop is installed but NOT running (starting it costs a lot of RAM). Local development uses the
  portable PostgreSQL 16 in `C:\SecureShiftTools\pgsql` (port 5434). Docker is used only for the manual run in Phase 6.
- Tools allowed without asking: VHS, ttyd, ffmpeg, portable PostgreSQL 16 → all in `C:\SecureShiftTools\`.

## Decisions (prototype vs DB, and other choices)

See the list at the end of this file; it is copied into the final report.

1. Super Admin user management shows the **Modernization** users only (the 4 roles in the prototype's
   `ROLES` list). The Cloud Admin account is not listed there, so the dashboard numbers match the prototype
   (24 users, 20 active, 4 inactive).
2. A Super Admin can never be deactivated or have the role changed (prototype rule). This also covers
   "cannot deactivate yourself" and "keep at least one active Super Admin".
3. No lockout (the prototype has none).

## Known bugs

(none yet)
