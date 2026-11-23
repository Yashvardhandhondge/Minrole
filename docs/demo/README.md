# SecureShift demo videos

The videos are **not** in the repository. They are attached to the GitHub pre-release **sprint4-demo**:
https://github.com/Yashvardhandhondge/Minrole/releases/tag/sprint4-demo

Recorded with [VHS](https://github.com/charmbracelet/vhs) in **Windows PowerShell** (like the TCS laptop), with the real
app and a real PostgreSQL database. Every typed key is visible (passwords show as `*****`). The `.tape` files in this
folder are the exact scripts; re-record with `powershell -ExecutionPolicy Bypass -File docs\demo\record.ps1`
(the files appear in this folder but Git ignores them).

| # | MP4 | GIF | Shows |
|---|---|---|---|
| 00 | [00-setup-tcs-laptop.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/00-setup-tcs-laptop.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/00-setup-tcs-laptop.gif) | fresh clone → portable PostgreSQL (no admin, no Docker) → connection string → build → db setup → db status → smoke → app |
| 01 | [01-super-admin.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/01-super-admin.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/01-super-admin.gif) | dashboard, users (search, Super Admin rule, add user with the on-screen temporary password, reset, deactivate), audit logs, profile |
| 02 | [02-cloud-admin-governance.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/02-cloud-admin-governance.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/02-cloud-admin-governance.gif) | Analysis Wizard with typed JSON paths: a bad file (friendly error), then the 3 sample files, results, identity detail, findings, generated role, report, notifications |
| 03 | [03-admin-modernization.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/03-admin-modernization.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/03-admin-modernization.gif) | register an enterprise, EMSI classification, register an application, component + dependency, dependency impact, digital twin, enterprise details |
| 04 | [04-architect.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/04-architect.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/04-architect.gif) | assessment with factors, simulation (Replatform), create + submit the plan (15/20/45/rest), My Plans |
| 05 | [05-executive.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/05-executive.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/05-executive.gif) | review, approve, reject (reason required), decision history |
| 06 | [06-full-workflow.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/06-full-workflow.mp4) | [gif](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/06-full-workflow.gif) | Super Admin → Admin → Architect → Executive → Cloud Admin in one session, then a live change made with psql shows on the next screen |

The same release also has **SecureShift-offline.zip** (the app without Git, without building, without internet):
see [docs/backend/TCS_LAPTOP_SETUP.md](../backend/TCS_LAPTOP_SETUP.md).

The live demo keystrokes (about 10 minutes, all roles) are in [DEMO_SCRIPT.md](DEMO_SCRIPT.md).

**The complete walkthrough** (every menu option of every role, every important error case, about 50 minutes, with
chapters and per-role videos) is in [full/](full/README.md) - release **demo-full**.
