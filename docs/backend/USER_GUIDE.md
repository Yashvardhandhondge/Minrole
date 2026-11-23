# SecureShift console – user guide (every menu, every role)

Start the app (see [TCS_LAPTOP_SETUP.md](TCS_LAPTOP_SETUP.md)):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1
```

**How the screens work**

- Menus are numbered. Type the number and press **Enter**. `0` = Back (or Logout on the first menu of a role).
- Lists show 10 rows per page: type a **row number** to open a row, `N` next page, `P` previous page,
  and the letters shown under the list (for example `S` search, `C` clear filters).
- In a form, the value in `[brackets]` is kept when you just press Enter. Type `cancel` to leave a form; nothing is saved.
- A wrong answer shows a red `[ERROR]` line with the reason, and the same question is asked again.
- Every screen reads fresh data from the database, so changes made by others (or in SQLTools / psql) show at once.
- If the Super Admin deactivates you (or changes your role) while you are logged in, the next action logs you out.
- No email is ever sent. Temporary passwords are shown on screen to the Super Admin, who shares them by hand.
- Times and dates are India time (`"App": { "TimeZone": "Asia/Kolkata" }` in appsettings.json): at 01:00 in India
  "Submitted On" and other dates already show the new day.

The examples below use the sample data (a fresh database: `run.ps1 -Reset`).

---

## Login screen (everyone)

```
SECURESHIFT · Modernize · Govern · Secure
=========================================
Welcome Back. Sign in to access your SecureShift account.
  1. Log in
  2. Forgot password?
  0. Exit
Choose: 1
Email: arya.mujumdar@secureshift.io
Password: **********
[OK] Signed in as Arya Mujumdar (Super Admin).
```

- Email is not case-sensitive. A wrong email or password always says `Invalid email or password.`
- An inactive account: `Your account is inactive. Please contact your Super Admin.`
- `2. Forgot password?` explains that the Super Admin resets the password and gives you a temporary one.
- **First login with a temporary password** (new user, or after a reset): you must choose a new password first.
  Rules: at least 8 characters with an uppercase letter, a lowercase letter, a number and a special character,
  and different from the temporary one.

Every role menu ends with **My Profile** (view, edit name / phone / department, change password; the Cloud Admin also
sees "My recent activity"), **Change Password**, and **0. Logout** (asks `Are you sure you want to logout ...? (y/n)`).

---

## Super Admin — arya.mujumdar@secureshift.io / Super@2026

```
  1. Dashboard
  2. User Management
  3. Audit Logs
  4. My Profile
  5. Change Password
  0. Logout
```

### 1. Dashboard
Type `1`. You see Total / Active / Inactive users (24 / 20 / 4), users by role with bars, the 5 latest actions and the
latest access changes (User Created, Role Changed, Account Deactivated, Password Reset ...).

### 2. User Management
Type `2`:

```
User Management - Users (24)
 #  ID       Full Name      Email                         Phone Number  Role         Status
 1  USR-001  Arya Mujumdar  arya.mujumdar@secureshift.io  9876543210    Super Admin  Active
 2  USR-002  Rohan Kapoor   rohan.kapoor@company.com      9876543211    Super Admin  Active
 ...
Showing 1-10 of 24 users · Page 1 of 3
Type a row number (#) to open a user, or: A add user · S search · R role filter · T status filter · C clear · N next · P previous · 0 back
```

| To do | Type |
|---|---|
| Search name / email / phone | `S`, then `kumar` |
| Only one role | `R`, then e.g. `4` (Architect) |
| Only Active / Inactive | `T`, then `2` / `3` |
| Clear the filters | `C` |
| **Add a user** | `A`, then Full Name `Riya Sen`, Email `riya.sen@company.com`, Phone `9123456789`, Role `3` (Architect) |
| Open a user | the row number, e.g. `3` |

After **Add**, the login details are shown once:

```
+--------------------------------------------------------------------------------+
|  User Created                                                                  |
+--------------------------------------------------------------------------------+
|  Name               : Riya Sen                                                 |
|  Role               : Architect                                                |
|  Email (login)      : riya.sen@company.com                                     |
|  Temporary password : Temp@6618x                                               |
|                                                                                |
|  Share these details with the user yourself. No email is sent.                 |
|  The password is shown only once. The user must change it at the first login.  |
+--------------------------------------------------------------------------------+
```

Rules (same as the prototype): name = letters and single spaces, 3-50; email valid and not used yet
(`Email already exists`); phone = 10 digits starting with 6, 7, 8 or 9; a role is required. New users are Active.

**User Details** (open a row) has: `1` Edit user (Enter keeps each value), `2` Change role (optional reason),
`3` Reset password (new temporary password in the same box, title "Password Reset"), `4` Deactivate / Activate
(asks `Are you sure ...? (y/n)` and an optional reason).
A Super Admin can never be deactivated or get another role (`A Super Admin cannot be deactivated or removed`),
and nobody can deactivate their own account.

### 3. Audit Logs
Newest first, 10 per page. `A` filter by action (type the number or the name, e.g. `Password Reset`), `U` by user,
`D` date range (MM/DD/YYYY, both days included, `-` = no limit), `S` search, `C` clear, `N` / `P` pages.

---

## Cloud Admin (Cloud Governance) — aarav.mehta@secureshift.io / Cloud@2026

```
  1. Dashboard
  2. Companies
  3. Run Analysis (Analysis Wizard)
  4. Previous Analyses
  5. Identities
  6. High-Risk Findings
  7. Generated Roles
  8. Rule Settings
  9. High-Risk Patterns
  10. Reports
  11. Notifications (1 unread)
  12. My Profile
  13. Change Password
  0. Logout
```

### 1. Dashboard
Latest analysis (company, period, identities, average score, open high-risk findings, roles waiting for review),
identities by risk (High 70-100 / Medium 40-69 / Low 0-39), the top 5 identities to fix, and the score trend.

### 2. Companies
Table of companies with the numbers of their latest analysis ("Not analysed" / PENDING when there is none).
`A` add company (Company Name ≥ 3 characters and new, Azure Tenant ID ≥ 8 characters, Subscriptions ≥ 1),
`S` search, `R` risk filter, a row number opens the company (`1` run a new analysis for it, `2` its identities).

### 3. Run Analysis (Analysis Wizard) — type the 3 JSON file paths

The sample files are in `samples\governance\` (formats: [samples/governance/README.md](../../samples/governance/README.md)).
The sample activity log covers **08/12/2026 - 11/09/2026**, so choose **4. Custom range**:

| Question | Type |
|---|---|
| Choose (menu) | `3` |
| Company | `1` (Northwind Traders) |
| Analysis period | `4` (Custom range), From `08/12/2026`, To `11/09/2026` |
| Role Assignments (JSON) | `samples\governance\role-assignments.json` |
| Activity Log (JSON) | `samples\governance\activity-log-90d.json` |
| Roles Catalog (JSON) | `samples\governance\roles-catalog.json` |
| Run the analysis now? | `y` |

(Pressing Enter on a file question uses the sample file shown in brackets.)

```
Analysis Wizard - Step 2 of 4: Validate
[OK] Schema validation - All 3 files match the expected JSON structure
[OK] Missing fields check - principalId, roleDefinitionId and scope present in every record
[OK] Duplicate data check - 0 duplicate role assignments found
[OK] Activity coverage - Activity Log covers 08/12/2026 - 11/09/2026 (90 days) for Northwind Traders
[OK] Validation passed
20 role assignments, 12 role definitions, 3714 events used (1 outside the period, 1 not successful, 1 from unknown callers - ignored)
...
Analysis Wizard - Step 4 of 4: Results
  Analysis ID                     : AN-2026-1115
  Company                         : Northwind Traders
  Period                          : 08/12/2026 - 11/09/2026 (90 days)
  Identities analysed             : 20
  Average score                   : 48.05
  High-risk unused permissions    : 186
  Least-privilege roles generated : 17
[OK] Analysis AN-2026-1115 saved. Next: Identities, High-Risk Findings or Generated Roles.
```

**Error demo:** use `samples\governance\bad\role-assignments-missing-fields.json` as the Role Assignments file:

```
[OK] Schema validation - All 3 files match the expected JSON structure
[ERROR] Missing fields check - role-assignments-missing-fields.json record 2 has no principalId, scope
Nothing was saved. Fix the file (or choose another one) and run the wizard again.
```

`samples\governance\bad\activity-log-broken.json` shows `... is not valid JSON (line 3, position 3)`; a path that does
not exist shows `file not found ... Check the path` and asks again. The whole result is saved in ONE transaction.

### How the Governance risk score is calculated (team to confirm)

> **Team to confirm.** The prototype only shows the 3 signals and their weights; it never calculates a score from files.
> This is the formula the console uses (code: `src/SecureShift.Console/Services/Governance/GovernanceScoring.cs`).
> The weights come from **Rule Settings** (default 50 / 30 / 20).

| Signal | Weight | How it is measured (0-100) |
|---|---|---|
| Unused permission ratio | 50% | `(1 - used / granted) x 100`, rounded |
| Unused high-risk actions | 30% | 10 points for every granted action that matches a high-risk pattern and was never used (max 100) |
| Scope breadth | 20% | Management Group or Subscription = 100, Resource Group = 40, one Resource = 10 |

**Score = (50 x ratio + 30 x high-risk + 20 x scope) / 100**, rounded.
Risk level: 70-100 HIGH, 40-69 MEDIUM, 0-39 LOW.

**Worked example – svc-webapp-deploy** (Contributor on the whole subscription, sample files):

- It was given 76 actions and used 14 of them → unused ratio = (1 - 14 / 76) x 100 = 81.6 → **82**
- 20 of its unused actions are high-risk (deletes, Key Vault, NSG writes) → 20 x 10 = 200 → capped at **100**
- Its scope is the whole Subscription → **100**
- Score = (50 x 82 + 30 x 100 + 20 x 100) / 100 = (4,100 + 3,000 + 2,000) / 100 = **91** → **HIGH**

(91 is also the score this identity has in the prototype's sample data.)

### 4. Previous Analyses
All analyses, newest first (ID, company, period, days, completed, identities, average score, status).

### 5. Identities
Identities of the latest analysis, highest score first. `S` search, `T` type, `R` risk, `A` other analysis,
`E` export CSV (saved in `exports\identities-export.csv`), a row number opens the **identity detail**:
score and risk, "Why this score" (unused permission ratio 50%, unused high-risk actions 30%, scope breadth 20%),
granted / used / unused, permissions used (first seen, last seen, events, resource group), unused high-risk permissions,
`1` its generated role, `2` export evidence JSON.

### 6. High-Risk Findings
Counts per category (Delete, Network (NSG), Key Vault, Role Assignment) and the list. `K` category, `I` identity,
`V` severity, `A` other analysis, `E` export CSV, a row number opens the identity.

### 7. Generated Roles
`X of Y reviewed`; open a role (row number):

```
  Role                  : svc-webapp-deploy-custom-role
  Status                : Pending review
  Original Role         : Contributor (Subscription)
  Suggested Scope       : rg-webapp-prod (Resource Group)
  Actions               : 12
  Rare / Review Actions (2) - used fewer than 3 times, confirm before keeping them in the role:
    - Microsoft.Web/sites/backup/action - used once, 70 days ago
    - Microsoft.Web/sites/delete - used once, 85 days ago
  Role definition (JSON):
    { "Name": "svc-webapp-deploy-custom-role", "IsCustom": true, ... }
  Azure CLI: az role definition create --role-definition svc-webapp-deploy-custom-role.json
  1. Mark as Reviewed
  2. Exclude baseline read actions
  3. Save role JSON file
```

### 8. Rule Settings
Weights (must add up to 100%), default analysis window (30 / 60 / 90 / 180 days), rare action threshold (1-50),
include baseline read actions. `1` edit (Enter keeps a value; a total ≠ 100 shows
`Weights must add up to 100% before saving`), `2` view rules.json, `3` reset to default (also resets the patterns).

### 9. High-Risk Patterns
`A` add a pattern (`*` = wildcard, e.g. `Microsoft.Sql/*/delete`; letters, numbers, `. / * -` only; no duplicates),
a row number → `1` enable / disable, `2` delete.

### 10. Reports
`G` generate a report for a company (uses its latest analysis), a row number previews it (Fix First top 3, score by
risk band) and can save the full report as HTML (`exports\RPT-....html`, open it in a browser to print).

### 11. Notifications
Newest first, `*` = unread; `y` marks all read. A new analysis adds 3 notifications.

---

## Admin (Modernization) — sachin.kumar@secureshift.io / Admin@2026

The Admin works in one **current enterprise** (shown under the title; change it with `3`).

```
  1. Dashboard (all enterprises)
  2. Register New Enterprise
  3. Choose current enterprise
  4. Enterprise Details (registration, EMSI, pricing)
  5. EMSI Classification
  6. Application Inventory
  7. Components & Dependencies
  8. Digital Twin
  9. My Profile
  10. Change Password
  0. Logout
```

### 1. Dashboard
Total / classified / pending enterprises, total applications, categories, and the enterprise table
(`S` search, `K` category, `R` register, a row number opens the enterprise).

### 2. Register New Enterprise
| Question | Type |
|---|---|
| Enterprise Name | `Tailspin Airlines` (3-100: letters, numbers, space, & . -; must be new) |
| Industry | `8` (Other) |
| Headquarters City | `Pune` |
| Contact Person | `Kavita Desai` |
| Contact Email | `kavita.desai@tailspin.com` |
| Contact Phone | `9812300011` |

`[OK] Tailspin Airlines registered as ENT-004. Next: classification (menu 5 - EMSI Classification).`

### 5. EMSI Classification (the 12 parameters)
| Question | Type |
|---|---|
| Total applications | `12` |
| Number of dependencies | `20` |
| Number of APIs | `8` |
| Lines of code | `250000` |
| Databases | `3` |
| Storage (GB) | `400` |
| Environments | `2` |
| Cloud infrastructure type | `1` (Public Cloud) |
| Business units | `2` |
| Active users | `30` |
| Technologies | `1,3` (Java + .NET) |
| Governance policies | `2,4` (HIPAA, ISO 27001) |
| Criticality Low / Medium / High / Critical | `3` / `4` / `3` / `2` (must add up to the total: 12) |

```
[OK] EMSI 32.33 - Growth category
EMSI 32.33 / 100 · Category: Growth
Scale: Foundation 0-25  [Growth 25-45]  Enterprise 45-70  Strategic 70-100
Growth Subscription Plan: Monthly ₹10,000 · Annual (-10%) ₹1,08,000
Parameter                  Your Input         Score  Weight  Weighted
Total applications         12                    20     14%       2.8
Number of dependencies     20                    25     14%       3.5
...
```

If the four criticality counts do not add up, you see
`Low + Medium + High + Critical must equal Total applications` and can fix the answers (they are kept as defaults).

### 4. Enterprise Details
KPIs, registration, EMSI ring + category scale + plan pricing, the inputs table. `1` edit registration, `2` classification.

### 6. Application Inventory
KPIs (total, critical, legacy, links) and the list (`S`, `T` technology, `K` criticality, `A` assessed).
`R` register an application — answers for the example: `Flight Booking`, `Sales`, `Web Team`, Status `1` (Active),
Deployment `3` (Public Cloud), Technology `1` (Java), Framework `Java 17`, Support `1` (Supported), Age `6`,
Documentation `1` (Good), Complexity `2` (Medium), Lines of code `300000`, Criticality `4` (Critical),
Sensitive data `1` (Yes), Security issues `1`, Downtime tolerance `1` (Low), Compliance policies `1`.
A row number shows the application (details, direct dependencies, latest assessment) with `1` edit (asks
`Confirm Update`) and `2` delete (also removes its components and links; not possible after an assessment).

### 7. Components & Dependencies
Components and links of the current enterprise.
`A` add component (Type `2` Database, Parent application, Name `Booking Database`), `X` delete component (only when
no link uses it), `L` add link (Source number, Target number, Criticality, Description), `E` edit link, `D` delete link,
`I` dependency impact per application (`100 x (1 - e^(-raw / 20))`), `S` search, `F` filter, `C` clear.
Link rules: source and target required and different, a component cannot depend on its own application,
no duplicate links.

### 8. Digital Twin
A text tree: every application with its components and every `depends on →` / `used by ←` link, plus the landscape
summary. `A` one application, `K` criticality, `E` another enterprise.

---

## Architect — amanjeet.sharma@secureshift.io / Architect@2026

```
  1. Dashboard
  2. Choose current enterprise
  3. Applications (read-only)
  4. Assessments
  5. Simulations
  6. Create Plan
  7. My Plans
  8. Digital Twin
```

### 4. Assessments
Choose the application (e.g. `2` = Customer360), then `1` Run Assessment:

```
Technical Debt: 47 / 100 · Medium
Application age        6 years               +12
Framework support      .NET 6 (Extended)     +12
Documentation quality  Average                +8
Code complexity        Medium                +10
Codebase size          4,50,000 lines         +5
Governance Risk: 72 / 100 · High
...
Dependency Impact: 59 / 100 · Medium
```

`2` Continue to Simulation (only for assessed applications).

### 5. Simulations
History of all simulations; `R` run a new one: choose the assessed application and a strategy
(`1` Rehost, `2` Replatform, `3` Refactor, `4` Rebuild):

```
SIM-007 · Customer360 · Replatform
  Dependency Impact  : 59 · Medium
  Strategy Risk      : 50
  Modernization Risk : 57 · Medium
  Estimated Effort   : 16 weeks
  Estimated Cost     : ₹14.11 lakh
Suggested order (most critical dependency first): Customer Database → Banking API → Customer360
  1. Create Plan from this simulation
  2. Discard / Modify (delete this simulation and run again)
```

### 6. Create Plan
Pick a simulation without a plan (or come from the simulation). The phases are fixed and read-only:
Assessment and Readiness 15%, Architecture and Design 20%, Implementation 45%, Testing and Validation = the rest,
always adding up to the effort (`Total estimated effort: 16 = 16 weeks`). Plan name (Enter keeps
`Customer360 Replatform Plan`; 5-80 characters, unique), notes (max 500), then `1` Save as Draft or
`2` Submit → `Submit modernization plan? (y/n)`. After submit the plan is locked.

### 7. My Plans
Counts per status and your plans (`T` status, `S` search). A Draft can be edited (`1`) or submitted (`2`).

---

## Executive — shivanand.kumatekar@secureshift.io / Exec@2026

```
  1. Plans awaiting review (3)
  2. All plans (filters)
  3. Decision history
```

Open a plan (row number) to see everything: scores, simulation snapshot (cost breakdown), application, architect notes,
affected systems, phases. For a **Submitted** plan:

- `1` Approve → comment (optional; empty = `Approved.`) → `Approve ... (₹22.15 lakh, 22 weeks)? (y/n)`
- `2` Reject → reason (at least 5 characters: `Please give a reason (at least 5 characters) to reject`) → confirm

`This decision cannot be changed later.` Drafts are never shown to the Executive. In `2. All plans`: `T` status,
`R` risk, `E` enterprise, `C` back to "Submitted". `3. Decision history` lists every approval / rejection with the
comment, who decided and when.
