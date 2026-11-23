# SecureShift demo videos

Recorded with [VHS](https://github.com/charmbracelet/vhs) in **Windows PowerShell** (like the TCS laptop), with the real
app and a real PostgreSQL database. Every typed key is visible (passwords show as `*****`). The `.tape` files in this
folder are the exact scripts; re-record with `powershell -ExecutionPolicy Bypass -File docs\demo\record.ps1`.

All videos (MP4 + GIF) are attached to the GitHub pre-release **sprint4-demo**:
https://github.com/Yashvardhandhondge/Minrole/releases/tag/sprint4-demo
Files under 10 MB are also in this folder.

| # | Video | Shows |
|---|---|---|
| 00 | [00-setup-tcs-laptop.mp4](00-setup-tcs-laptop.mp4) | fresh clone → portable PostgreSQL (no admin, no Docker) → connection string → build → db setup → db status → smoke → app |
| 01 | [01-super-admin.mp4](01-super-admin.mp4) | dashboard, users (search, Super Admin rule, add user with the on-screen temporary password, reset, deactivate), audit logs, profile |
| 02 | [02-cloud-admin-governance.mp4](02-cloud-admin-governance.mp4) | Analysis Wizard with typed JSON paths: a bad file (friendly error), then the 3 sample files, results, identity detail, findings, generated role, report, notifications |
| 03 | [03-admin-modernization.mp4](03-admin-modernization.mp4) | register an enterprise, EMSI classification, register an application, component + dependency, dependency impact, digital twin, enterprise details |
| 04 | [04-architect.mp4](04-architect.mp4) | assessment with factors, simulation (Replatform), create + submit the plan (15/20/45/rest), My Plans |
| 05 | [05-executive.mp4](05-executive.mp4) | review, approve, reject (reason required), decision history |
| 06 | [06-full-workflow.mp4](06-full-workflow.mp4) | Super Admin → Admin → Architect → Executive → Cloud Admin in one session, then a live change made with psql shows on the next screen |

GIF versions: same names with `.gif`. `03-admin-modernization.gif` (10.1 MB) and `06-full-workflow.gif` (21 MB) are over 10 MB, so they are only in the release.

The live demo keystrokes (about 10 minutes, all roles) are in [DEMO_SCRIPT.md](DEMO_SCRIPT.md).
