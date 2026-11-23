# SecureShift: Sprint 1 Prototype (v2)

**Secure Your Cloud. Modernize Your Applications.**
TCS Training · Cloud Domain · Group 3 (Nimbus) + Group 4 (ModernOps)

| Product | Team | What it does |
|---|---|---|
| **Cloud Governance** | Group 3 · Nimbus | Compares the Azure permissions an identity was *given* with what it *used* in a fixed analysis period, gives a 0-100 Over-Privilege Score and builds a least-privilege custom role. |
| **Application Modernization (EMDT)** | Group 4 · ModernOps | Onboards enterprises, classifies them with the EMSI score, maps application dependencies, assesses Technical Debt and Governance Risk, simulates modernization and sends plans to an Executive for approval. |

## How to run the Sprint 4 console app (C# .NET 8 + PostgreSQL)

The `src/` folder holds the complete console version of SecureShift (all 5 roles) on a PostgreSQL database.

| I want to ... | Read |
|---|---|
| set it up on a laptop that has only .NET 8 (no Docker, no admin) | [docs/backend/TCS_LAPTOP_SETUP.md](docs/backend/TCS_LAPTOP_SETUP.md) |
| run it with no Git, no build, no internet | `SecureShift-offline.zip` in the [sprint4-demo release](https://github.com/Yashvardhandhondge/Minrole/releases/tag/sprint4-demo) (paths B-D in TCS_LAPTOP_SETUP.md) |
| know every menu and the exact keys to type | [docs/backend/USER_GUIDE.md](docs/backend/USER_GUIDE.md) |
| the database commands, the faculty DB, tests, schema changes | [docs/backend/RUNNING.md](docs/backend/RUNNING.md) |
| give a 10-minute live demo / watch the videos | [docs/demo/DEMO_SCRIPT.md](docs/demo/DEMO_SCRIPT.md), [docs/demo/](docs/demo/) |
| watch EVERY menu option of every role (the complete walkthrough video, with chapters) | [docs/demo/full/](docs/demo/full/) (release [demo-full](https://github.com/Yashvardhandhondge/Minrole/releases/tag/demo-full)) |
| the sprint status, decisions and fixed bugs | [docs/backend/PROGRESS.md](docs/backend/PROGRESS.md) |

Quick start (Windows PowerShell, in this folder):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1
```

It starts a portable PostgreSQL 16 (port 5434), creates the tables and sample data the first time, and opens the
login screen. Log in with one of the 5 accounts below (the console uses the same accounts as the prototype).

---

**Sprint 1 rules:** only HTML, CSS and JavaScript. No backend, no libraries, no CDN. All demo data is hardcoded in `js/data/`. Changes are saved in the browser (localStorage).

---

## 1. How to run

1. Open the `secure-shift` folder in **VS Code**.
2. Right-click `index.html` and choose **Open with Live Server**. (Double-clicking also works.)
3. Click **Login**, type one of the emails and passwords below, and press **LOGIN**. You land on the dashboard of that role.

### The 5 login accounts (each has its own password)

| Role | Email | Password | Goes to |
|---|---|---|---|
| Cloud Admin | aarav.mehta@secureshift.io | `Cloud@2026` | Cloud Governance dashboard |
| Super Admin | arya.mujumdar@secureshift.io | `Super@2026` | Super Admin dashboard (Modernization) |
| Admin | sachin.kumar@secureshift.io | `Admin@2026` | Admin dashboard (Modernization) |
| Architect | amanjeet.sharma@secureshift.io | `Architect@2026` | Architect dashboard (Modernization) |
| Executive | shivanand.kumatekar@secureshift.io | `Exec@2026` | Executive approvals (Modernization) |

If a user opens another role's page (for example the Architect types the Admin page address), they are sent back to their own dashboard with a message. A password changed on the Profile page works at the next login.

**To reset the demo data:** press F12, run `localStorage.clear()` in the Console, then refresh.

---

## 2. What changed in v2

**Cloud Governance**
- The name "MinRole" is removed everywhere (folder `minrole/` is now `governance/`).
- Removed the **Revoke** button (findings are read-only, with a *View Identity* link).
- The analysis runs on a **fixed period**: last 30/60/90 days or a custom From-To range (7 to 180 days, not in the future).
- **Grey blocks**: KPI cards are grey, card headers have grey bands, and wizard steps you haven't reached yet are greyed out. The whole UI got more CSS polish.
- Identities show the **user name** (display name plus `user@northwindtraders.com`).
- Removed the **CI/CD Gate** (page, menu item and dashboard card).

**Modernization (EMDT)**
- Notifications removed (no bell icon, no notification or activity tabs).
- **Super Admin:** strict validations; Add User has no status field; Super Admin can promote to Super Admin but never demote or deactivate one; no CSV export on Audit Logs.
- **Admin:** Dashboard lists **enterprises**, with Register New Enterprise. Each enterprise has a details page, a **Classification (EMSI)** tab (category + pricing), Dependencies (components + links + scores), Digital Twin and Application Inventory. All cost fields are removed.
- **Architect:** Dashboard lists enterprises. Menu is Dashboard, Applications, Digital Twin, Assessments, Simulations, Plans and Logout. The Applications list is read-only. The assessment shows only Technical Debt and Governance Risk (6 factors). The simulation offers 4 strategies only. Plans can only be created from a simulation, with a read-only summary and 4 generated phases.
- **Executive:** 3 submitted demo plans to approve. Statuses are Draft, Submitted, Approved and Rejected.

---

## 2a. What changed in v3 (colour theme)

The UI is no longer black and white. (Since v5 both modules share ONE theme: indigo / violet. The teal Modernization colour below was removed.)

| Where | Colour |
| --- | --- |
| Cloud Governance | Indigo / purple (sidebar, buttons, table headers) |
| Modernization (EMDT) | Same indigo / violet as Governance (v5) |
| KPI cards | Each card in a row has its own colour: blue, green, amber, violet, pink |
| Badges | Red = bad (HIGH, REJECTED), amber = needs attention (MEDIUM, SUBMITTED), green = good (LOW, APPROVED), blue = info (DRAFT) |
| Score bars | Red = high score, amber = medium, green = low |
| Digital Twin nodes | Application = indigo, Service = blue, Database = amber, API = pink |

Grey is kept only for locked or disabled parts (steps not reached yet, disabled buttons, the Assessment results before you run them).

How it works: all colours are CSS variables in `css/base.css` (`--accent`, `--red`, `--green` and so on).
Both modules use the same `--accent`, so every page looks like one project.

| File | Owner | What changed |
| --- | --- | --- |
| `css/base.css`, `css/layout.css` | Ashish | Colour variables, buttons, cards, KPIs, badges, tables, coloured sidebar |
| `governance/css/governance.css` | Ashish | Coloured wizard stepper, upload boxes, checks |
| `css/landing.css`, `js/pages/login.js` | Yash | Coloured landing and login; the login card turns teal when you pick Modernization |
| `modernization/css/emdt.css`, `modernization/digital-twin.html`, `modernization/js/digital-twin.js` | Mohit Choudhary | Coloured graph nodes, EMSI ring, plan timeline |
| `js/common/ui.js`, `modernization/js/simulation.js` | Shrinivas | Badge colours, coloured line and donut charts |
| `js/common/shell.js` | Ashish | The Profile page takes the colour of the product you logged in to |

## 2b. What changed in v4 (Modernization review, round 2)

1. **Live input validation.** On every Modernization form, a red message appears below a field while you type if the value breaks a rule, and disappears when you fix it. Helper: `SS.ui.live(form, rules)` in `js/common/ui.js`.
2. **Admin: no formulas on screen.** Removed the EMSI score breakdown table, the technology formula, the dependency points guide, the points preview and the raw points column. The maths still runs in `emdt-calc.js`.
3. **Architect, Simulation strategies.** Only the 4 strategy names and a one-line description; the "Strategy Risk · Effort · Team" line is removed.
4. **Architect, after Run Simulation.** The Calculation Breakdown card is removed; only *Discard / Modify* and *Create Plan from this Simulation* remain.

## 2c. What changed in v5 (one theme, real logins, user-created popup)

1. **One unified theme.** Governance and Modernization now use the same colours (indigo / violet), fonts, spacing and components. Only colours changed, no logic.
2. **Login page cleanup.** No demo account buttons and no Governance / Modernization tabs. You type an email and password.
3. **5 accounts, 5 roles, 5 passwords.** See the table in section 1.
4. **Role-based redirection.** After login each user goes to their own dashboard. Opening another role's page sends you back to your dashboard with a message.
5. **Super Admin, Add User.** No Active / Inactive field (new users are Active). After a successful Create User, a popup confirms it (since v7 it shows the login details to share manually, see 2e). If saving fails, an error message shows and the form stays open.

**Files changed in v5 and why:**

| File | Owner | Why |
| --- | --- | --- |
| `css/base.css` | Ashish | Removed the separate teal Modernization theme; one theme for all pages |
| `css/landing.css` | Yash | Landing and login use the same indigo / violet theme; white logo icon |
| `index.html` | Yash | Login links no longer pick a product; FAQ no longer shows a demo password |
| `login.html`, `js/pages/login.js` | Yash | Removed demo accounts and product tabs; login by email + password; go to the role's dashboard |
| `js/data/users.js` | Yash | 5 accounts, each with its own password; `SS.passwordOf()` / `SS.setPassword()` |
| `js/common/core.js` | Yash | `SS.guard` sends a user who opens another role's page back to their own dashboard |
| `js/common/shell.js` | Ashish | Shows the "You do not have access to that page" message after that redirect |
| `js/pages/profile.js` | Shivanand | Change Password checks the real current password and saves the new one |
| `modernization/js/users.js` | Arya | Popup after creating a user; error handling if saving fails |
| `modernization/js/digital-twin.js` | Mohit Choudhary | Application node text colour matches the unified theme |

## 2d. What changed in v6 (Phase 2: database + layout fixes)

**New `database/` folder (SQL Server 2019+ / Azure SQL Database):**

| File | What it does |
| --- | --- |
| `database/01_create_schema.sql` | Creates SecureShiftDB: 3 schemas (`auth`, `gov`, `emdt`), 28 tables, all CHECK / UNIQUE / FOREIGN KEY rules, 4 views |
| `database/02_seed_data.sql` | 569 rows of sample data, the same data the screens show (one transaction) |
| `database/03_sample_queries.sql` | Example queries for the Phase 3 API: login, users, dashboards, approvals |

Run them in order in SSMS or Azure Data Studio. The full design (every table and column) is in the "SecureShift Database Design (Phase 2)" doc, and the full project guide (front end, database, ER diagram, keys, who did what) is in the "SecureShift Complete Guide" doc.

`database/diagrams/` has the ER diagrams as PNG (to view) and SVG (to zoom without blur): `er_workflow` (tables and relationships), `er_governance` and `er_modernization` (every column and key), `er_full` (all 28 tables), plus `fe_architecture.png` (how the front end loads).

**Layout fixes (buttons no longer overflow):** checked on every page at 1440, 1280, 1024 and 768 px wide.

| File | Owner | Fix |
| --- | --- | --- |
| `css/base.css` | Ashish | Table action buttons stay on one line and stick to the right edge; long codes wrap; button rows wrap |
| `css/layout.css` | Ashish | Page-heading buttons wrap instead of overflowing |
| `governance/css/governance.css` | Ashish | Long rule patterns wrap (delete button stays in the card); upload-box buttons wrap |
| `modernization/css/emdt.css` | Mohit Choudhary | EMSI category boxes fit narrow cards |

## 2e. What changed in v7 (no email sending)

The team decided not to send any emails. Login details are now given to users **manually**.

| Where | Before | Now |
| --- | --- | --- |
| Super Admin > Add User | Popup: "Account information has been sent to ..." | Popup shows name, role, login email and a **temporary password** (e.g. `Temp@2919x`) with a **Copy Details** button. The Super Admin shares it in person, by phone or chat. |
| Super Admin > Reset Password | Toast: "Password reset link sent to ..." | Same popup with a new temporary password to give the user |
| Login > Forgot Password | Asked for email, "Send Reset Link" | Message: "Please contact your Super Admin" |
| Landing FAQ | "You receive your login details by email" | "The Super Admin gives you your login details directly" |
| Profile > Notifications | Had "Email alerts" | Removed |
| Database | Table `auth.EmailNotifications` | Table removed. Now **28 tables**, 569 seed rows |

| File | Owner |
| --- | --- |
| `modernization/js/users.js` | Arya |
| `js/pages/login.js`, `index.html` | Yash |
| `js/pages/profile.js` | Shivanand |
| `js/data/emdt-data.js` | Mohit Choudhary |
| `database/01_create_schema.sql`, `02_seed_data.sql`, `03_sample_queries.sql`, `database/diagrams/*` | Yash |

## 3. Folder structure (72 code files + this README)

```
secure-shift/
├── README.md
├── index.html  login.html  logout.html  profile.html
├── css/            base.css  layout.css  landing.css
├── js/
│   ├── common/     core.js  ui.js  shell.js
│   ├── data/       users.js  governance-data.js  emdt-data.js  emdt-calc.js
│   └── pages/      landing.js  login.js  logout.js  profile.js
├── governance/                      ===== Group 3 · Nimbus =====
│   ├── css/governance.css
│   ├── dashboard.html  analysis.html  identities.html  identity-detail.html
│   ├── findings.html   roles.html     rules.html       reports.html  companies.html
│   └── js/  (one .js per page above, same name)
└── modernization/                   ===== Group 4 · ModernOps =====
    ├── css/emdt.css
    ├── superadmin-dashboard.html  users.html  audit-logs.html
    ├── admin-dashboard.html  enterprise.html  classification.html
    ├── dependencies.html  digital-twin.html  applications.html  register-application.html
    ├── architect-dashboard.html  assessments.html  simulation.html  plans.html  create-plan.html
    ├── executive-dashboard.html  review-plan.html
    └── js/  (one .js per page above, same name)
```

Create these empty folders first: `css`, `js/common`, `js/data`, `js/pages`, `governance/css`, `governance/js`, `modernization/css`, `modernization/js`.

---

## 4. Who commits which files

### Group 3 · Nimbus (Cloud Governance)

| # | Member | Files | Feature |
|---|---|---|---|
| 1 | **Yashwardhan Dhondge** | `README.md`, `index.html`, `login.html`, `logout.html`, `css/landing.css`, `js/common/core.js`, `js/data/users.js`, `js/pages/landing.js`, `js/pages/login.js`, `js/pages/logout.js` | Landing, Login, Logout, session + validation core |
| 2 | **Ashish Kumar** | `css/base.css`, `css/layout.css`, `js/common/shell.js`, `governance/css/governance.css`, `governance/dashboard.html`, `governance/js/dashboard.js` | Design system (grey blocks), app frame + menus, Governance Dashboard |
| 3 | **Ashwini Rathod** | `governance/analysis.html`, `governance/js/analysis.js`, `governance/companies.html`, `governance/js/companies.js` | Analysis Wizard (fixed period), Companies |
| 4 | **Mohit Deore** | `js/data/governance-data.js`, `governance/identities.html`, `governance/js/identities.js`, `governance/identity-detail.html`, `governance/js/identity-detail.js` | Governance data, Identities (user names), Identity Detail & Evidence |
| 5 | **Suhas Sambargi** | `governance/findings.html`, `governance/js/findings.js`, `governance/roles.html`, `governance/js/roles.js` | High-Risk Findings, Generated Roles |
| 6 | **Vedant Mahanavar** | `governance/rules.html`, `governance/js/rules.js`, `governance/reports.html`, `governance/js/reports.js` | Rules Configuration, Reports |

### Group 4 · ModernOps (EMDT)

| # | Member | Files | Feature |
|---|---|---|---|
| 7 | **Arya Mujumdar** | `modernization/superadmin-dashboard.html`, `modernization/js/superadmin-dashboard.js`, `modernization/users.html`, `modernization/js/users.js`, `modernization/audit-logs.html`, `modernization/js/audit-logs.js` | Super Admin: Dashboard, User Management, Audit Logs |
| 8 | **Sachin Kumar** | `modernization/admin-dashboard.html`, `modernization/js/admin-dashboard.js`, `modernization/enterprise.html`, `modernization/js/enterprise.js`, `modernization/classification.html`, `modernization/js/classification.js` | Admin: Enterprise list + registration, Enterprise Details, Classification (EMSI) |
| 9 | **Mohit Choudhary** | `js/data/emdt-data.js`, `modernization/css/emdt.css`, `modernization/dependencies.html`, `modernization/js/dependencies.js`, `modernization/digital-twin.html`, `modernization/js/digital-twin.js` | EMDT data model, Dependency Mapping, Digital Twin |
| 10 | **Amanjeet Sharma** | `modernization/applications.html`, `modernization/js/applications.js`, `modernization/register-application.html`, `modernization/js/register-application.js`, `modernization/architect-dashboard.html`, `modernization/js/architect-dashboard.js`, `modernization/plans.html`, `modernization/js/plans.js` | Application Inventory, Register/Edit App, Architect Dashboard, Plans list |
| 11 | **Shrinivas Naik** | `js/common/ui.js`, `js/data/emdt-calc.js`, `modernization/assessments.html`, `modernization/js/assessments.js`, `modernization/simulation.html`, `modernization/js/simulation.js` | Shared UI components, all EMDT formulas, Assessment, Simulation |
| 12 | **Shivanand Kumatekar** | `modernization/create-plan.html`, `modernization/js/create-plan.js`, `modernization/executive-dashboard.html`, `modernization/js/executive-dashboard.js`, `modernization/review-plan.html`, `modernization/js/review-plan.js`, `profile.html`, `js/pages/profile.js` | Create Plan, Executive Approval, Review Plan, Profile & Settings |

### Commit order

**Round 1 (shared foundation, commit first):**
1. Ashish Kumar: `css/base.css`, `css/layout.css`, `js/common/shell.js`
2. Yashwardhan Dhondge: `js/common/core.js`, `js/data/users.js`
3. Shrinivas Naik: `js/common/ui.js`, `js/data/emdt-calc.js`
4. Mohit Deore: `js/data/governance-data.js`
5. Mohit Choudhary: `js/data/emdt-data.js`, `modernization/css/emdt.css`

**Round 2:** everyone commits the rest of their files.

---

## 5. EMDT formulas (all in `js/data/emdt-calc.js`)

**EMSI** = Σ (parameter score × weight). Weights: Total apps 14%, Dependencies 14%, APIs 7%, Databases & storage 9%, Environments 6%, Business units 4%, Technology diversity 15%, Active users 4%, Application criticality 9%, Governance policies 6%, Cloud type 7%, Lines of code 5%.
Technology diversity = (sum of selected values ÷ 740) × 100, where Java = 75, PHP = 95, .NET = 120 and COBOL = 450.

| Category | EMSI | Effort multiplier | Monthly | Annual |
|---|---|---|---|---|
| Foundation | 0-25 | ×0.90 | ₹5,000 | ₹54,000 |
| Growth | 25-45 | ×1.00 | ₹10,000 | ₹1,08,000 |
| Enterprise | 45-70 | ×1.05 | ₹25,000 | ₹2,70,000 |
| Strategic | 70-100 | ×1.10 | ₹50,000 | ₹5,40,000 |

**Dependency points** = Type weight (Application 4, Service 3, Database 3, API 2) × Criticality (Low 1, Medium 2, High 3, Critical 4). Only direct outgoing links count.
**Dependency Impact** = 100 × (1 − e^(−raw ÷ 20)).
**Technical Debt** = Age (max 25) + Framework support (Unsupported 25, Extended 12, Supported 0) + Documentation (15/8/0) + Complexity (20/10/3) + Codebase size (2-15).
**Governance Risk** = Criticality (30/22/12/5) + Sensitive data (20) + Security issues (0/5/10/15) + Highest dependency criticality (15/10/5/2) + Downtime tolerance (Low 10, Medium 5, High 0) + Compliance policies (0/5/10).
**Modernization Risk** = TD×0.30 + GR×0.25 + DI×0.25 + Strategy Risk×0.20. Strategy Risk: Rehost 25, Replatform 50, Refactor 75, Rebuild 100.
**Effort (weeks)** = (6 + MR×0.12) × EMSI multiplier + strategy weeks (Rehost +1, Replatform +2, Refactor +4, Rebuild +6).
**Cost (₹ lakh)** = (team × person-months × 0.60 + testing 10% + tools 1.00 + other 1.50) × 1.15 contingency. Team: Rehost 3, Replatform 4, Refactor 5, Rebuild 6.
**Phases** = 15% / 20% / 45% / remainder, so they always add up to the effort.

Check (the example from the doc): Trade Finance Portal + Refactor gives TD 95, GR 90, DI 83, MR 87, **22 weeks** and **₹22.15 lakh**.

> Assumptions to confirm with the trainer, because these parts were not visible in the shared document: the EMSI category ranges for Foundation and Growth, the effort multipliers, the Dependency Impact normalisation constant (20), and the base effort and cost constants. Each one is a single number at the top of `emdt-calc.js`.

---

## 6. Demo flow for the review

**Cloud Governance:** Dashboard → Analysis Wizard (Last 60 days, run) → Identities (search "Rahul Verma") → Identity detail and evidence → Generated Role → High-Risk Findings → Reports.

**EMDT (full loop):**
1. **Super Admin:** add a user (try a wrong name or phone to show the validation), promote a user to Super Admin, try to deactivate a Super Admin (blocked).
2. **Admin:** Register New Enterprise → Classification (Save & Calculate EMSI) → Application Inventory (register 2 apps) → Dependencies (add a component and links, see the scores) → Digital Twin.
3. **Architect:** Dashboard → open Northwind Bank → Assess Customer360 → Continue to Simulation → Trade Finance Portal + Refactor → Create Plan → Submit.
4. **Executive:** Approval Dashboard → Review → Approve, or Reject with a comment.
5. **Architect again:** Plans shows the Executive's comment.

**Tested:** 148 automated click checks across all roles, with 0 JavaScript errors.
