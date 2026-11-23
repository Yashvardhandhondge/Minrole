#!/usr/bin/env node
/*
 * The COMPLETE SecureShift video walkthrough, written as data: every clip, every key that is typed,
 * what must appear on the screen, and which menu option each step covers.
 *
 *   node docs/demo/full/clips.js list                       the clips and their titles
 *   node docs/demo/full/clips.js coverage                   every menu option of the checklist is covered? (exit 1 if not)
 *   node docs/demo/full/clips.js dryrun [--from N] [--to N]  runs the clips WITHOUT recording, against the real app and
 *                                                            database (same keys, same order, same data), checks every
 *                                                            expected text, writes transcripts/ + layout.json
 *   node docs/demo/full/clips.js tapes                      writes tapes/NN-role-task.tape (VHS) from the clips + layout.json
 *   node docs/demo/full/clips.js docs <timings.json>        writes COVERAGE.md, CHAPTERS.md, chapters-youtube.txt and the .srt
 *
 * Everything runs in C:\Demo (outside the repository); scripts/windows/record-full-demo.ps1 drives the whole thing.
 */
"use strict";
const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const HERE = __dirname;
const DEMO = "C:\\Demo";                 // demo root (outside the repository)
const PKG = DEMO + "\\SecureShift";      // the unzipped offline package (the app every clip runs)
const LIVE = DEMO + "\\live";            // the SQL files of the live-change clips (copied from sql/)
const TOOLS = "C:\\SecureShiftTools";
const COLS = 131, ROWS = 35;             // the VHS terminal at FontSize 25, 1920x1080, Padding 24 (measured)
const TYPING = 0.07;                     // seconds per typed key (Set TypingSpeed 70ms)
const APP = "dotnet app\\SecureShift.Console.dll";

/* =====================================================================================================
 * 1. The checklist: EVERY menu option of EVERY role (+ the important error cases).
 *    [id, role, menu path, expected result]
 * ===================================================================================================== */
const OPTIONS = [
  // ---- setup (offline package)
  ["setup.unzip", "Setup", "Offline package: unzip + PostgreSQL zip from the pen drive", "SecureShift folder with app\\, run.ps1, samples\\ ...; the pg zip next to run.ps1"],
  ["setup.runps1.setup", "Setup", "run.ps1 -SetupOnly", "portable PostgreSQL unzipped + started on port 5434, db setup: 28 tables, 4 views, 569 rows"],
  ["setup.dbstatus", "Setup", "db status", "connection OK, 28/28 tables, 4/4 views, baseline recorded"],
  ["setup.smoke", "Setup", "smoke", "the 5 login accounts and the main tables answer: [OK]"],
  ["setup.runps1", "Setup", "run.ps1 (start the app)", "PostgreSQL already running, tables there, login screen"],
  // ---- login screen
  ["login.wrongnum", "Login", "Login screen > a wrong number", "[ERROR] Type 1, 2 or 0."],
  ["login.forgot", "Login", "Login screen > 2. Forgot password?", "contact your Super Admin (no email is sent)"],
  ["login.empty", "Login", "Login screen > 1. Log in (empty email + password)", "[ERROR] Please enter your email and password."],
  ["login.bademail", "Login", "Login screen > 1. Log in (invalid email)", "[ERROR] Please enter a valid email address."],
  ["login.wrongpw", "Login", "Login screen > 1. Log in (wrong password)", "[ERROR] Invalid email or password."],
  ["login.unknown", "Login", "Login screen > 1. Log in (unknown email)", "[ERROR] Invalid email or password. (does not reveal which part is wrong)"],
  ["login.inactive", "Login", "Login screen > 1. Log in (Inactive user)", "[ERROR] Your account is inactive. Please contact your Super Admin."],
  ["login.forced.cancel", "Login", "Forced password change > cancel", "[INFO] Logged out. Your password was not changed."],
  ["login.forced.rule", "Login", "Forced password change (wrong current, weak new, mismatch)", "[ERROR] Current password is wrong · Min 8 characters ... · Passwords do not match"],
  ["login.forced.same", "Login", "Forced password change (new = current)", "[ERROR] New password must be different from the current password"],
  ["login.forced", "Login", "Forced password change (MustChangePassword)", "[OK] Password updated, then the user's own menu"],
  ["login.ok", "Login", "Login screen > 1. Log in (new password)", "Signed in directly, no forced change any more"],
  ["login.logout.no", "Login", "Menu > 0. Logout > n", "stays logged in, the menu comes back"],
  ["login.logout.yes", "Login", "Menu > 0. Logout > y", "[OK] You have been logged out (audit: Logged Out)"],
  ["login.exit", "Login", "Login screen > 0. Exit", "Goodbye."],
  ["login.legacy", "Login", "Log in with a legacy SHA-256 password hash", "[INFO] password upgraded to the new secure format"],
  ["session.deactivated", "Login", "Deactivated while logged in", "next menu choice: logged out with a warning"],
  // ---- Super Admin
  ["SA.1", "Super Admin", "1. Dashboard", "totals, users by role, recent activity, recent access changes"],
  ["SA.2", "Super Admin", "2. User Management", "users table, 10 per page"],
  ["SA.users.N", "Super Admin", "User Management > N next page", "page 2 of 3, page 3 of 3"],
  ["SA.users.P", "Super Admin", "User Management > P previous page", "back one page"],
  ["SA.users.S", "Super Admin", "User Management > S search", "only matching users"],
  ["SA.users.R", "Super Admin", "User Management > R role filter", "only that role"],
  ["SA.users.T", "Super Admin", "User Management > T status filter", "only Active / Inactive users"],
  ["SA.users.C", "Super Admin", "User Management > C clear", "all users again"],
  ["SA.users.unknown", "Super Admin", "User Management > unknown choice", "[ERROR] Unknown choice. Type a row number, A, S, R, T, C, N, P or 0."],
  ["SA.users.row", "Super Admin", "User Management > row number", "User Details with the 4 actions"],
  ["SA.users.0", "Super Admin", "User Management > 0 back", "back to the Super Admin menu"],
  ["SA.add.validation", "Super Admin", "User Management > A add user (invalid name, email, phone, no role)", "[ERROR] under each field, the question is asked again"],
  ["SA.add", "Super Admin", "User Management > A add user", "[OK] created + box with the temporary password Temp@NNNNx (no email)"],
  ["SA.add.dup", "Super Admin", "User Management > A add user (duplicate email)", "[ERROR] Email: Email already exists"],
  ["SA.add.cancel", "Super Admin", "User Management > A add user > cancel", "[INFO] Cancelled. Nothing was saved."],
  ["SA.detail.1", "Super Admin", "User Details > 1. Edit user", "[OK] updated (Enter keeps a value)"],
  ["SA.detail.2", "Super Admin", "User Details > 2. Change role", "[OK] ... is now Admin"],
  ["SA.detail.3", "Super Admin", "User Details > 3. Reset password", "box with a new temporary password, Must change password = Yes"],
  ["SA.detail.4d", "Super Admin", "User Details > 4. Deactivate user", "[OK] deactivated, the menu now offers Activate"],
  ["SA.detail.4a", "Super Admin", "User Details > 4. Activate user", "[OK] activated"],
  ["SA.detail.0", "Super Admin", "User Details > 0 back", "back to the users table"],
  ["SA.rule.superadmin", "Super Admin", "User Details of a Super Admin > 4. Deactivate", "[ERROR] A Super Admin cannot be deactivated or removed"],
  ["SA.rule.rolelocked", "Super Admin", "User Details of a Super Admin > 2. Change role", "[ERROR] the Super Admin role cannot be changed"],
  ["SA.rule.superedit", "Super Admin", "User Details of a Super Admin > 1. Edit user", "Role: Super Admin (cannot be removed), details saved"],
  ["SA.rule.self", "Super Admin", "User Details of yourself > 4. Deactivate", "[ERROR] You cannot deactivate your own account"],
  ["SA.3", "Super Admin", "3. Audit Logs", "newest first, 10 per page"],
  ["SA.audit.N", "Super Admin", "Audit Logs > N next page", "next page"],
  ["SA.audit.P", "Super Admin", "Audit Logs > P previous page", "previous page"],
  ["SA.audit.A", "Super Admin", "Audit Logs > A action filter", "only that action"],
  ["SA.audit.U", "Super Admin", "Audit Logs > U user filter", "only that user"],
  ["SA.audit.D.err", "Super Admin", "Audit Logs > D date range (From after To)", "[ERROR] From date must be before To date"],
  ["SA.audit.D", "Super Admin", "Audit Logs > D date range", "only that period"],
  ["SA.audit.S", "Super Admin", "Audit Logs > S search", "only matching rows"],
  ["SA.audit.C", "Super Admin", "Audit Logs > C clear", "all rows again"],
  ["SA.audit.unknown", "Super Admin", "Audit Logs > unknown choice", "[ERROR] Unknown choice. Type A, U, D, S, C, N, P or 0."],
  ["SA.audit.0", "Super Admin", "Audit Logs > 0 back", "back to the menu"],
  ["SA.wrongnum", "Super Admin", "Super Admin menu > a wrong number", "[ERROR] Type a number from 0 to 5."],
  ["SA.4", "Super Admin", "4. My Profile", "Profile & Settings: details of the logged-in user"],
  ["SA.5", "Super Admin", "5. Change Password (wrong current password)", "[ERROR] Current password is wrong (nothing changed)"],
  // ---- Cloud Admin
  ["CA.1", "Cloud Admin", "1. Dashboard", "latest analysis, KPIs, identities by risk, top 5, score trend"],
  ["CA.2", "Cloud Admin", "2. Companies", "companies with identities, avg score, high-risk, risk level"],
  ["CA.comp.S", "Cloud Admin", "Companies > S search", "only matching companies"],
  ["CA.comp.R", "Cloud Admin", "Companies > R risk filter", "only that risk level"],
  ["CA.comp.C", "Cloud Admin", "Companies > C clear", "all companies"],
  ["CA.comp.A.err", "Cloud Admin", "Companies > A add company (invalid name / tenant)", "[ERROR] Enter at least 3 characters · Enter a valid tenant ID"],
  ["CA.comp.A", "Cloud Admin", "Companies > A add company", "[OK] Margie Travel added"],
  ["CA.comp.A.dup", "Cloud Admin", "Companies > A add company (duplicate)", "[ERROR] Company name: This company already exists"],
  ["CA.comp.N", "Cloud Admin", "Companies > N next page", "page 2 of 2"],
  ["CA.comp.P", "Cloud Admin", "Companies > P previous page", "page 1 of 2"],
  ["CA.comp.row", "Cloud Admin", "Companies > row number", "company detail: KPIs, tenant, 1 Run New Analysis, 2 View Identities"],
  ["CA.comp.detail.2", "Cloud Admin", "Company > 2. View Identities", "identities of that company's latest analysis"],
  ["CA.comp.detail.2.err", "Cloud Admin", "Company without analysis > 2. View Identities", "[ERROR] ... has no analysis yet. Run the Analysis Wizard first."],
  ["CA.comp.detail.0", "Cloud Admin", "Company > 0 back", "back to the companies table"],
  ["CA.comp.unknown", "Cloud Admin", "Companies > unknown choice", "[ERROR] Unknown choice ..."],
  ["CA.comp.0", "Cloud Admin", "Companies > 0 back", "back to the menu"],
  ["CA.wiz.period.err", "Cloud Admin", "Analysis Wizard > period (too short / in the future)", "[ERROR] Period must be at least 7 days · To date cannot be in the future"],
  ["CA.wiz.err.missing", "Cloud Admin", "Analysis Wizard > JSON path that does not exist", "[ERROR] file not found (asked again)"],
  ["CA.wiz.err.notjson", "Cloud Admin", "Analysis Wizard > a file that is not .json", "[ERROR] please choose a .json file (asked again)"],
  ["CA.wiz.err.broken", "Cloud Admin", "Analysis Wizard > broken JSON file", "[ERROR] Schema validation - not valid JSON (line 3 ...) · Nothing was saved"],
  ["CA.wiz.err.fields", "Cloud Admin", "Analysis Wizard > records with missing fields", "[ERROR] Missing fields check - record 2 has no principalId, scope"],
  ["CA.wiz.err.shape", "Cloud Admin", "Analysis Wizard > JSON of the wrong shape (object, not list)", "[ERROR] must be a JSON array of role assignments ... it is a JSON object"],
  ["CA.wiz.preset", "Cloud Admin", "Analysis Wizard > period preset (Last 90 days)", "Period: the last 90 days up to yesterday"],
  ["CA.wiz.cancel", "Cloud Admin", "Analysis Wizard > Run the analysis now? n", "[INFO] Cancelled. Nothing was saved."],
  ["CA.comp.detail.1", "Cloud Admin", "Company > 1. Run New Analysis", "the wizard with the company already chosen"],
  ["CA.wiz.paths", "Cloud Admin", "Analysis Wizard > the 3 JSON paths (typed relative, typed absolute, Enter = sample)", "Uploaded: role-assignments.json, activity-log-90d.json, roles-catalog.json"],
  ["CA.wiz.run", "Cloud Admin", "Analysis Wizard > Validate + Run + Results", "4 checks [OK], 7 stages, results: analysis ID, identities, average score, roles"],
  ["CA.4", "Cloud Admin", "4. Previous Analyses", "every analysis with period, identities, average score"],
  ["CA.5", "Cloud Admin", "5. Identities", "identities of the latest analysis with granted / used / score / risk"],
  ["CA.id.N", "Cloud Admin", "Identities > N next page", "next page"],
  ["CA.id.P", "Cloud Admin", "Identities > P previous page", "previous page"],
  ["CA.id.S", "Cloud Admin", "Identities > S search", "only matching identities"],
  ["CA.id.T", "Cloud Admin", "Identities > T type filter", "only that identity type"],
  ["CA.id.R", "Cloud Admin", "Identities > R risk filter", "only that risk band"],
  ["CA.id.C", "Cloud Admin", "Identities > C clear", "all identities"],
  ["CA.id.E", "Cloud Admin", "Identities > E export CSV", "[OK] Saved N identities to ...identities-export.csv"],
  ["CA.id.A", "Cloud Admin", "Identities > A other analysis", "the identities of the chosen analysis"],
  ["CA.id.row", "Cloud Admin", "Identities > row number", "identity: why this score, permissions used, unused high-risk"],
  ["CA.id.detail.1", "Cloud Admin", "Identity > 1. View generated role", "the least-privilege role of this identity"],
  ["CA.id.detail.2", "Cloud Admin", "Identity > 2. Export evidence (JSON)", "[OK] Saved ...-evidence.json"],
  ["CA.id.unknown", "Cloud Admin", "Identities > unknown choice", "[ERROR] Unknown choice ..."],
  ["CA.id.0", "Cloud Admin", "Identities > 0 back", "back to the menu"],
  ["CA.6", "Cloud Admin", "6. High-Risk Findings", "unused high-risk actions with totals per category"],
  ["CA.fd.N", "Cloud Admin", "Findings > N next page", "next page"],
  ["CA.fd.P", "Cloud Admin", "Findings > P previous page", "previous page"],
  ["CA.fd.K", "Cloud Admin", "Findings > K category filter", "only that category"],
  ["CA.fd.V", "Cloud Admin", "Findings > V severity filter", "only that severity"],
  ["CA.fd.I", "Cloud Admin", "Findings > I identity filter", "only that identity"],
  ["CA.fd.C", "Cloud Admin", "Findings > C clear", "all findings"],
  ["CA.fd.E", "Cloud Admin", "Findings > E export CSV", "[OK] Saved N findings to ...high-risk-findings.csv"],
  ["CA.fd.A", "Cloud Admin", "Findings > A other analysis", "findings of the chosen analysis"],
  ["CA.fd.row", "Cloud Admin", "Findings > row number", "the identity of that finding"],
  ["CA.fd.unknown", "Cloud Admin", "Findings > unknown choice", "[ERROR] Unknown choice ..."],
  ["CA.fd.0", "Cloud Admin", "Findings > 0 back", "back to the menu"],
  ["CA.7", "Cloud Admin", "7. Generated Roles", "least-privilege roles, reviewed / pending"],
  ["CA.gr.N", "Cloud Admin", "Generated Roles > N next page", "next page"],
  ["CA.gr.P", "Cloud Admin", "Generated Roles > P previous page", "previous page"],
  ["CA.gr.row", "Cloud Admin", "Generated Roles > row number", "role: rare actions, role definition JSON, az CLI command"],
  ["CA.gr.1", "Cloud Admin", "Generated Role > 1. Mark as Reviewed / Pending", "[OK] marked as reviewed / moved back to pending"],
  ["CA.gr.2", "Cloud Admin", "Generated Role > 2. Exclude / Include baseline read actions", "[OK] Saved. (the JSON changes)"],
  ["CA.gr.3", "Cloud Admin", "Generated Role > 3. Save role JSON file", "[OK] Saved ...json"],
  ["CA.gr.back", "Cloud Admin", "Generated Role > 0 back", "back"],
  ["CA.gr.A", "Cloud Admin", "Generated Roles > A other analysis", "roles of the chosen analysis"],
  ["CA.gr.unknown", "Cloud Admin", "Generated Roles > unknown choice", "[ERROR] Unknown choice ..."],
  ["CA.gr.0", "Cloud Admin", "Generated Roles > 0 back", "back to the menu"],
  ["CA.8", "Cloud Admin", "8. Rule Settings", "weights, window, rare threshold, baseline"],
  ["CA.rules.2", "Cloud Admin", "Rule Settings > 2. View rules.json", "the rules.json with the current values"],
  ["CA.rules.1.err", "Cloud Admin", "Rule Settings > 1. Edit (weights not 100%)", "[ERROR] Weights must add up to 100% before saving"],
  ["CA.rules.1", "Cloud Admin", "Rule Settings > 1. Edit weights and settings", "inline errors for bad numbers, [OK] rules.json saved"],
  ["CA.rules.3", "Cloud Admin", "Rule Settings > 3. Reset to default", "[OK] Rules reset to default"],
  ["CA.rules.wrong", "Cloud Admin", "Rule Settings > a wrong number", "[ERROR] Type 1, 2, 3 or 0."],
  ["CA.rules.0", "Cloud Admin", "Rule Settings > 0 back", "back to the menu"],
  ["CA.9", "Cloud Admin", "9. High-Risk Patterns", "patterns with Enabled Yes/No"],
  ["CA.pat.A.err", "Cloud Admin", "Patterns > A add (invalid characters)", "[ERROR] Only letters, numbers, . / * - are allowed"],
  ["CA.pat.A", "Cloud Admin", "Patterns > A add pattern", "[OK] Pattern added"],
  ["CA.pat.A.dup", "Cloud Admin", "Patterns > A add (duplicate) > cancel", "[ERROR] This pattern already exists, then cancelled"],
  ["CA.pat.row.1", "Cloud Admin", "Patterns > row > 1. Disable / Enable", "[OK] Pattern disabled / enabled"],
  ["CA.pat.row.2", "Cloud Admin", "Patterns > row > 2. Delete", "[OK] Pattern removed"],
  ["CA.pat.unknown", "Cloud Admin", "Patterns > unknown choice", "[ERROR] Unknown choice. Type a row number, A or 0."],
  ["CA.pat.0", "Cloud Admin", "Patterns > 0 back", "back to the menu"],
  ["CA.10", "Cloud Admin", "10. Reports", "reports with identities, average score, high-risk"],
  ["CA.rep.G", "Cloud Admin", "Reports > G generate new report", "[OK] RPT-... generated for the company"],
  ["CA.rep.G.err", "Cloud Admin", "Reports > G for a company without analysis", "[ERROR] ... has no analysis yet"],
  ["CA.rep.row", "Cloud Admin", "Reports > row number (preview) > save HTML", "Fix First list, risk bands, [OK] Saved ...html"],
  ["CA.rep.unknown", "Cloud Admin", "Reports > unknown choice", "[ERROR] Unknown choice. Type a row number, G or 0."],
  ["CA.rep.0", "Cloud Admin", "Reports > 0 back", "back to the menu"],
  ["CA.11", "Cloud Admin", "11. Notifications > mark all read", "unread marked *, [OK] All notifications marked as read"],
  ["CA.12", "Cloud Admin", "12. My Profile > 3. My recent activity", "profile + the user's last 10 actions (Cloud Governance only)"],
  ["CA.13", "Cloud Admin", "13. Change Password (wrong current password)", "[ERROR] Current password is wrong (nothing changed)"],
  ["CA.wrongnum", "Cloud Admin", "Cloud Admin menu > a wrong number", "[ERROR] Type a number from 0 to 13."],
  // ---- Admin
  ["AD.1", "Admin", "1. Dashboard (all enterprises)", "totals, categories, enterprises table with EMSI + plan"],
  ["AD.dash.S", "Admin", "Dashboard > S search", "only matching enterprises"],
  ["AD.dash.K", "Admin", "Dashboard > K category filter", "only that category (Pending = not classified)"],
  ["AD.dash.C", "Admin", "Dashboard > C clear", "all enterprises"],
  ["AD.dash.N", "Admin", "Dashboard > N / P paging", "Page 1 of 1 (stays)"],
  ["AD.dash.row", "Admin", "Dashboard > row number", "Enterprise Details of that enterprise"],
  ["AD.dash.R", "Admin", "Dashboard > R register", "the Register New Enterprise form"],
  ["AD.dash.unknown", "Admin", "Dashboard > unknown choice", "[ERROR] Unknown choice ..."],
  ["AD.dash.0", "Admin", "Dashboard > 0 back", "back to the menu"],
  ["AD.2.err", "Admin", "2. Register New Enterprise (duplicate name, bad city / email / phone, no industry)", "[ERROR] under each field, asked again"],
  ["AD.2", "Admin", "2. Register New Enterprise", "[OK] Tailspin Airlines registered as ENT-004"],
  ["AD.3", "Admin", "3. Choose current enterprise", "[OK] Current enterprise: ..."],
  ["AD.4", "Admin", "4. Enterprise Details", "registration, EMSI, category, pricing"],
  ["AD.4.1", "Admin", "Enterprise Details > 1. Edit registration", "[OK] Registration details saved"],
  ["AD.4.2", "Admin", "Enterprise Details > 2. Add / Edit classification details", "the 12 EMSI parameters (cancel = nothing saved)"],
  ["AD.4.0", "Admin", "Enterprise Details > 0 back", "back"],
  ["AD.5.err", "Admin", "5. EMSI Classification (bad numbers, no technology, levels not matching)", "[ERROR] inline, then EMSI not calculated + fix list"],
  ["AD.5", "Admin", "5. EMSI Classification", "[OK] EMSI 32.33 - Growth category + parameter table + plan price"],
  ["AD.6", "Admin", "6. Application Inventory", "KPIs + applications table"],
  ["AD.app.R.err", "Admin", "Inventory > R register (invalid name / framework / age / LOC)", "[ERROR] under each field, asked again"],
  ["AD.app.R", "Admin", "Inventory > R register application", "[OK] Flight Booking registered as APP-011"],
  ["AD.app.R.dup", "Admin", "Inventory > R register (duplicate name) > cancel", "[ERROR] This application already exists in Tailspin Airlines"],
  ["AD.app.S", "Admin", "Inventory > S search", "only matching applications"],
  ["AD.app.T", "Admin", "Inventory > T technology filter", "only that technology"],
  ["AD.app.K", "Admin", "Inventory > K criticality filter", "only that criticality"],
  ["AD.app.A", "Admin", "Inventory > A assessment filter", "Assessed / Not Assessed"],
  ["AD.app.C", "Admin", "Inventory > C clear", "all applications"],
  ["AD.app.N", "Admin", "Inventory > N / P paging", "Page 1 of 1 (stays)"],
  ["AD.app.row", "Admin", "Inventory > row number", "application details + direct dependencies"],
  ["AD.app.row.1", "Admin", "Application > 1. Edit application", "Enter keeps values, Confirm Update, [OK] updated"],
  ["AD.app.row.2", "Admin", "Application > 2. Delete application", "its links + components are removed too, [OK] deleted"],
  ["AD.app.row.0", "Admin", "Application > 0 back", "back to the inventory"],
  ["AD.app.unknown", "Admin", "Inventory > unknown choice", "[ERROR] Unknown choice ..."],
  ["AD.app.0", "Admin", "Inventory > 0 back", "back to the menu"],
  ["AD.7", "Admin", "7. Components & Dependencies", "components table + dependency links table"],
  ["AD.dep.A", "Admin", "Components > A add component (no type first)", "[ERROR] Select a type, then [OK] Database Booking Database added"],
  ["AD.dep.L", "Admin", "Dependencies > L add link", "[OK] Dependency DEP-0xx saved"],
  ["AD.dep.L.dup", "Admin", "Dependencies > L add link (duplicate)", "[ERROR] Target: This link already exists"],
  ["AD.dep.L.self", "Admin", "Dependencies > L add link (source = target)", "[ERROR] Target: Source and target cannot be the same"],
  ["AD.dep.L.parent", "Admin", "Dependencies > L add link (component -> own application)", "[ERROR] A component cannot depend on its own parent application"],
  ["AD.dep.E", "Admin", "Dependencies > E edit link", "[OK] Dependency DEP-0xx updated"],
  ["AD.dep.D", "Admin", "Dependencies > D delete link", "[OK] Dependency deleted"],
  ["AD.dep.X", "Admin", "Components > X delete component", "[OK] Component deleted"],
  ["AD.dep.I", "Admin", "Dependencies > I impact results", "Dependency Impact per application = 100 x (1 - e^(-raw/20))"],
  ["AD.dep.S", "Admin", "Dependencies > S search", "only matching links"],
  ["AD.dep.F", "Admin", "Dependencies > F filter (type + criticality)", "only matching links"],
  ["AD.dep.C", "Admin", "Dependencies > C clear", "all links"],
  ["AD.dep.unknown", "Admin", "Dependencies > unknown choice", "[ERROR] Unknown choice. Type A, X, L, E, D, I, S, F, C or 0."],
  ["AD.dep.0", "Admin", "Dependencies > 0 back", "back to the menu"],
  ["AD.8", "Admin", "8. Digital Twin", "every application with components and links (text view)"],
  ["AD.twin.A", "Admin", "Digital Twin > A one application", "only that application"],
  ["AD.twin.K", "Admin", "Digital Twin > K criticality filter", "only links of that criticality"],
  ["AD.twin.C", "Admin", "Digital Twin > C clear", "everything again"],
  ["AD.twin.E", "Admin", "Digital Twin > E choose enterprise", "the twin of another enterprise"],
  ["AD.twin.unknown", "Admin", "Digital Twin > unknown choice", "[ERROR] Type A, K, C, E or 0."],
  ["AD.twin.0", "Admin", "Digital Twin > 0 back", "back to the menu"],
  ["AD.9", "Admin", "9. My Profile", "Profile & Settings"],
  ["AD.10", "Admin", "10. Change Password (wrong current password)", "[ERROR] Current password is wrong (nothing changed)"],
  ["AD.wrongnum", "Admin", "Admin menu > a wrong number", "[ERROR] Type a number from 0 to 10."],
  // ---- Architect
  ["AR.1", "Architect", "1. Dashboard", "enterprises, assessed apps, recent assessments + simulations, my plans"],
  ["AR.2", "Architect", "2. Choose current enterprise", "[OK] Current enterprise: Tailspin Airlines"],
  ["AR.3", "Architect", "3. Applications (read-only)", "applications of the enterprise with assessment status"],
  ["AR.app.S", "Architect", "Applications > S search", "only matching applications"],
  ["AR.app.A", "Architect", "Applications > A assessment filter", "Assessed / Not Assessed"],
  ["AR.app.C", "Architect", "Applications > C clear", "all applications"],
  ["AR.app.N", "Architect", "Applications > N / P paging", "Page 1 of 1 (stays)"],
  ["AR.app.unknown", "Architect", "Applications > unknown choice", "[ERROR] Unknown choice ..."],
  ["AR.app.row", "Architect", "Applications > row number", "application details, 1 Assess, 2 Simulate"],
  ["AR.app.row.1", "Architect", "Application > 1. Assess", "the assessment screen of that application"],
  ["AR.app.row.2", "Architect", "Application > 2. Simulate", "a new simulation of that application"],
  ["AR.app.0", "Architect", "Applications > 0 back", "back to the menu"],
  ["AR.4", "Architect", "4. Assessments (pick an application)", "assessment screen"],
  ["AR.asm.2.err", "Architect", "Assessment > 2. Continue to Simulation (not assessed)", "[ERROR] Run the assessment first"],
  ["AR.asm.1", "Architect", "Assessment > 1. Run Assessment", "[OK] Assessment ASM-0xx saved: TD, GR + factor tables + Dependency Impact"],
  ["AR.asm.1r", "Architect", "Assessment > 1. Recalculate Assessment", "new assessment, history shows both"],
  ["AR.asm.2", "Architect", "Assessment > 2. Continue to Simulation", "the strategy question"],
  ["AR.asm.0", "Architect", "Assessment > 0 back", "back"],
  ["AR.5", "Architect", "5. Simulations", "all simulations with risk, effort, cost, plan"],
  ["AR.sim.rehost", "Architect", "Simulations > R run > 1 Rehost", "[OK] Simulation SIM-0xx complete + result"],
  ["AR.sim.replatform", "Architect", "Simulations > R run > 2 Replatform", "[OK] Simulation complete + result"],
  ["AR.sim.refactor", "Architect", "Simulations > R run > Enter = Refactor (default)", "[OK] Simulation complete + result"],
  ["AR.sim.rebuild", "Architect", "Simulations > R run > 4 Rebuild", "[OK] Simulation complete + result"],
  ["AR.sim.discard", "Architect", "Simulation result > 2. Discard / Modify", "[INFO] Simulation discarded"],
  ["AR.sim.row", "Architect", "Simulations > row number", "the saved simulation result"],
  ["AR.sim.res.0", "Architect", "Simulation result > 0 back", "back to the list"],
  ["AR.sim.res.1", "Architect", "Simulation result > 1. Create Plan from this simulation", "the Create Plan screen"],
  ["AR.sim.res.open", "Architect", "Simulation result (has a plan) > 1. Open plan", "the plan (read-only)"],
  ["AR.sim.discard.err", "Architect", "Simulation result (has a plan) > 2. Discard", "[ERROR] ... already has a plan, so it cannot be discarded"],
  ["AR.sim.unknown", "Architect", "Simulations > unknown choice", "[ERROR] Type R, a row number or 0."],
  ["AR.sim.0", "Architect", "Simulations > 0 back", "back to the menu"],
  ["AR.6", "Architect", "6. Create Plan (choose a simulation)", "summary, snapshot, affected systems, phases 15% / 20% / 45% / rest = effort"],
  ["AR.plan.name.err", "Architect", "Create Plan > plan name already used", "[ERROR] A plan with this name already exists"],
  ["AR.plan.1", "Architect", "Create Plan > 1. Save as Draft", "[OK] PLAN-0xx saved as draft"],
  ["AR.plan.2n", "Architect", "Create Plan > 2. Submit > n", "[INFO] Not submitted. Nothing was saved."],
  ["AR.plan.2", "Architect", "Create Plan > 2. Submit for approval > y", "[OK] PLAN-0xx submitted for approval (locked)"],
  ["AR.plan.0", "Architect", "Create Plan > 0. Cancel", "[INFO] Cancelled. Nothing was saved."],
  ["AR.7", "Architect", "7. My Plans", "counts per status + my plans"],
  ["AR.my.T", "Architect", "My Plans > T status filter", "only that status"],
  ["AR.my.S", "Architect", "My Plans > S search", "only matching plans"],
  ["AR.my.C", "Architect", "My Plans > C clear", "all my plans"],
  ["AR.my.draft.1", "Architect", "My Plans > Draft > 1. Edit name and notes", "[OK] PLAN-0xx saved as draft"],
  ["AR.my.draft.2", "Architect", "My Plans > Draft > 2. Submit for approval", "[OK] PLAN-0xx submitted for approval"],
  ["AR.my.locked", "Architect", "My Plans > a submitted plan", "This plan is read-only. Waiting for Executive review ... locked"],
  ["AR.my.unknown", "Architect", "My Plans > unknown choice", "[ERROR] Unknown choice. Type a row number, T, S, C or 0."],
  ["AR.my.0", "Architect", "My Plans > 0 back", "back to the menu"],
  ["AR.8", "Architect", "8. Digital Twin", "the twin of the current enterprise"],
  ["AR.9", "Architect", "9. My Profile", "Profile & Settings"],
  ["AR.10", "Architect", "10. Change Password (wrong current password)", "[ERROR] Current password is wrong (nothing changed)"],
  ["AR.wrongnum", "Architect", "Architect menu > a wrong number", "[ERROR] Type a number from 0 to 10."],
  // ---- Executive
  ["EX.1", "Executive", "1. Plans awaiting review", "KPIs + submitted plans (drafts are never shown)"],
  ["EX.list.R", "Executive", "Plans > R risk filter", "only that risk level"],
  ["EX.list.E", "Executive", "Plans > E enterprise filter", "only that enterprise"],
  ["EX.list.T", "Executive", "Plans > T status filter", "All / Submitted / Approved / Rejected"],
  ["EX.list.C", "Executive", "Plans > C clear", "back to Submitted plans"],
  ["EX.list.N", "Executive", "Plans > N / P paging", "Page 1 of 1 (stays)"],
  ["EX.list.unknown", "Executive", "Plans > unknown choice", "[ERROR] Unknown choice ..."],
  ["EX.review", "Executive", "Plans > row number (review)", "scores, snapshot, affected systems, phases, architect notes"],
  ["EX.review.1", "Executive", "Review > 1. Approve (comment optional)", "[OK] PLAN-0xx approved"],
  ["EX.review.2.err", "Executive", "Review > 2. Reject without a reason", "[ERROR] Please give a reason (at least 5 characters) to reject"],
  ["EX.review.2", "Executive", "Review > 2. Reject with a reason", "[OK] PLAN-0xx rejected"],
  ["EX.review.n", "Executive", "Review > Approve > n", "[INFO] No decision was saved."],
  ["EX.review.0", "Executive", "Review > 0 back", "back to the list"],
  ["EX.list.0", "Executive", "Plans > 0 back", "back to the menu"],
  ["EX.2", "Executive", "2. All plans (filters)", "all submitted / approved / rejected plans"],
  ["EX.decided", "Executive", "All plans > an approved plan", "Executive decision: Approved by ... (no Approve / Reject any more)"],
  ["EX.3", "Executive", "3. Decision history", "every decision with comment, newest first"],
  ["EX.race", "Executive", "Review open while another executive decides (psql)", "[ERROR] PLAN-001 was already approved. The decision cannot be changed."],
  ["EX.4", "Executive", "4. My Profile", "Profile & Settings"],
  ["EX.prof.1.err", "Executive", "My Profile > 1. Edit details (invalid phone)", "[ERROR] Exactly 10 digits, starting with 6, 7, 8 or 9"],
  ["EX.prof.1", "Executive", "My Profile > 1. Edit details", "[OK] Profile updated"],
  ["EX.prof.2.err", "Executive", "My Profile > 2. Change password (wrong current + weak new)", "[ERROR] Current password is wrong · Min 8 characters ..."],
  ["EX.prof.2", "Executive", "My Profile > 2. Change password", "[OK] Password updated"],
  ["EX.prof.0", "Executive", "My Profile > 0 back", "back to the menu"],
  ["EX.5", "Executive", "5. Change Password", "[OK] Password updated (back to the original password)"],
  ["EX.wrongnum", "Executive", "Executive menu > a wrong number", "[ERROR] Type a number from 0 to 5."],
  // ---- live database change
  ["live.users", "Live DB change", "psql -f changes a user while the user list is open", "C (refresh) shows the new name at once"],
];
const OPT = new Map(OPTIONS.map(([id, role, menu, expected]) => [id, { id, role, menu, expected }]));

/* =====================================================================================================
 * 2. The clips. Small DSL:
 *    k(text, opts)    one line typed into the app. opts: c = coverage id(s) that START here, x = expected text(s) in
 *                     the output, err = this step shows an [ERROR] on purpose, pw = password (shown as *****),
 *                     p = pause in seconds after the output (default: 2 for a screen, 3 = important result)
 *    sh(text, opts)   a PowerShell command (outside the app)
 * ===================================================================================================== */
const k = (text, o = {}) => ({ t: "key", text: String(text), ...o });
const sh = (text, o = {}) => ({ t: "sh", text, ...o });
const login = (email, pw, o = {}) => [k("1"), k(email), k(pw, { pw: true, p: 2, x: o.x ?? "Signed in as", ...o })];
const logoutExit = () => [k("0", { c: "login.logout.yes" }), k("y", { x: "You have been logged out" }), k("0", { x: "Goodbye." })];
const SAMPLES = "samples\\governance\\";
const wrongPw = (menuNo, id) => [
  k(menuNo, { c: id }), k("Wrong@2026", { pw: true }), k("Better@2026", { pw: true }), k("Better@2026", { pw: true, err: true, x: "Current password is wrong" }),
];

const USERS = {
  arya: ["arya.mujumdar@secureshift.io", "Super@2026"],
  aarav: ["aarav.mehta@secureshift.io", "Cloud@2026"],
  sachin: ["sachin.kumar@secureshift.io", "Admin@2026"],
  aman: ["amanjeet.sharma@secureshift.io", "Architect@2026"],
  shiva: ["shivanand.kumatekar@secureshift.io", "Exec@2026"],
};

const CLIPS = [
  /* ------------------------------------------------------------------ 0. setup from zero, offline */
  {
    n: 0, file: "00-setup-offline-package", step: 0, role: "Setup", task: "Setup from zero with the offline package", video: "01-setup-and-login",
    small: "Only .NET 8 on the laptop: unzip, PostgreSQL zip from the pen drive, run.ps1, db status, smoke, login screen",
    shell: true, cwd: DEMO,
    items: [
      sh("# SecureShift from zero: a laptop with only .NET 8 - no Git, no build, no internet, no admin rights", { p: 1 }),
      sh("dotnet --list-runtimes", { c: "setup.unzip" }),
      sh("Get-ChildItem C:\\Demo\\PenDrive | Format-Table Name, Length -AutoSize"),
      sh("Expand-Archive C:\\Demo\\PenDrive\\SecureShift-offline.zip -DestinationPath C:\\Demo", { timeout: 300, p: 1 }),
      sh("Copy-Item C:\\Demo\\PenDrive\\postgresql-16.11-1-windows-x64-binaries.zip C:\\Demo\\SecureShift", { timeout: 120, p: 1 }),
      sh("cd C:\\Demo\\SecureShift", { p: 0.5 }),
      sh("Get-ChildItem | Format-Table Name, Length -AutoSize"),
      sh("powershell -ExecutionPolicy Bypass -File run.ps1 -SetupOnly", { c: "setup.runps1.setup", timeout: 900, p: 3 }),
      sh("$env:ConnectionStrings__SecureShiftDb = Get-Content C:\\SecureShiftTools\\connection-string.txt", { p: 0.5 }),
      sh(APP + " db status", { c: "setup.dbstatus", timeout: 120, p: 3 }),
      sh(APP + " smoke", { c: "setup.smoke", timeout: 120, p: 3 }),
      sh("powershell -ExecutionPolicy Bypass -File run.ps1", { c: "setup.runps1", timeout: 300, p: 3, keys: [k("0", { c: "login.exit", x: "Goodbye." })] }),
    ],
  },
  /* ------------------------------------------------------------------ 1. login errors + forced change */
  {
    n: 1, file: "01-login-errors-forced-change", step: 1, role: "Login", task: "Login errors and the forced password change", video: "01-setup-and-login",
    small: "Wrong number, forgot password, empty / invalid / wrong / unknown login, Inactive user, temporary password must be changed",
    items: [
      k("9", { c: "login.wrongnum", err: true, x: "Type 1, 2 or 0." }),
      k("2", { c: "login.forgot", x: "Please contact your Super Admin." }),
      k("1", { c: "login.empty" }), k(""), k("", { pw: true, err: true, x: "Please enter your email and password." }),
      k("1", { c: "login.bademail" }), k("arya.mujumdar"), k("Super@2026", { pw: true, err: true, x: "Please enter a valid email address." }),
      k("1", { c: "login.wrongpw" }), k("arya.mujumdar@secureshift.io"), k("Wrong@2026", { pw: true, err: true, x: "Invalid email or password." }),
      k("1", { c: "login.unknown" }), k("nobody@secureshift.io"), k("Super@2026", { pw: true, err: true, x: "Invalid email or password." }),
      k("1", { c: "login.inactive" }), k("sakshi.patil@company.com"), k("Welcome@2026", { pw: true, err: true, x: "Your account is inactive" }),
      k("1", { c: "login.forced.cancel" }), k("mohit.choudhary@company.com"), k("Welcome@2026", { pw: true, x: "You are using a temporary password" }),
      k("cancel", { pw: true, x: "Logged out. Your password was not changed." }),
      k("1", { c: "login.forced.rule" }), k("mohit.choudhary@company.com"), k("Welcome@2026", { pw: true }),
      k("Welcome@2025", { pw: true }), k("mohit123", { pw: true }), k("mohit124", { pw: true, err: true, x: ["Current password is wrong", "Passwords do not match"] }),
      k("Welcome@2026", { pw: true, c: "login.forced.same" }), k("Welcome@2026", { pw: true }), k("Welcome@2026", { pw: true, err: true, x: "must be different" }),
      k("Welcome@2026", { pw: true, c: "login.forced" }), k("Mohit@2026", { pw: true }), k("Mohit@2026", { pw: true, p: 3, x: ["Password updated", "Signed in as Mohit Choudhary (Architect)"] }),
      k("0", { c: "login.logout.no" }), k("n", { x: "Architect" }),
      k("0", { c: "login.logout.yes" }), k("y", { x: "You have been logged out" }),
      k("1", { c: "login.ok" }), k("mohit.choudhary@company.com"), k("Mohit@2026", { pw: true, x: "Signed in as Mohit Choudhary" }),
      k("0"), k("y"),
      k("0", { c: "login.exit", x: "Goodbye." }),
    ],
  },
  /* ------------------------------------------------------------------ 2. Super Admin */
  {
    n: 2, file: "02-super-admin-dashboard-users", step: 2, role: "Super Admin", task: "Dashboard and the user list", video: "02-super-admin", user: "arya",
    small: "Dashboard, users table: next / previous page, search, role + status filters, clear, open a user",
    items: [
      k("1", { c: "SA.1", p: 3, x: "Super Admin Dashboard" }),
      k("2", { c: "SA.2", x: "User Management - Users" }),
      k("N", { c: "SA.users.N", x: "Page 2 of 3" }), k("N", { x: "Page 3 of 3" }),
      k("P", { c: "SA.users.P", x: "Page 2 of 3" }),
      k("S", { c: "SA.users.S" }), k("kumar", { x: "Search: kumar" }),
      k("C", { c: "SA.users.C", x: "Page 1 of 3" }),
      k("R", { c: "SA.users.R" }), k("Architect", { x: "Role: Architect" }),
      k("T", { c: "SA.users.T" }), k("Inactive", { x: "Status: Inactive" }),
      k("C"),
      k("Z", { c: "SA.users.unknown", err: true, x: "Unknown choice" }),
      k("3", { c: "SA.users.row", x: "User Details" }),
      k("0", { c: "SA.detail.0" }),
      k("0", { c: "SA.users.0" }),
      k("9", { c: "SA.wrongnum", err: true, x: "Type a number from 0 to 5." }),
    ],
  },
  {
    n: 3, file: "03-super-admin-add-user", step: 2, role: "Super Admin", task: "Add a user: validation, temporary password, duplicate, cancel", video: "02-super-admin", user: "arya",
    small: "Every field is checked; the temporary password is shown on screen once (no email)",
    items: [
      k("2"),
      k("A", { c: "SA.add.validation", x: "Add New User" }),
      k("Nisha V3rma", { err: true, x: "Only letters and single spaces" }), k("Nisha Verma"),
      k("nisha.verma@company", { err: true, x: "Enter a valid email" }), k("nisha.verma@company.com"),
      k("12345", { err: true, x: "Exactly 10 digits" }), k("9811223344"),
      k("", { err: true, x: "Select a role" }),
      k("Architect", { c: "SA.add", p: 3, x: ["created as USR-026", "Temporary password : Temp@"] }),
      k("A", { c: "SA.add.dup" }), k("Arya Second"), k("arya.mujumdar@secureshift.io"), k("9876501234"), k("Admin", { err: true, x: "Email already exists" }),
      k("A", { c: "SA.add.cancel" }), k("cancel", { x: "Cancelled. Nothing was saved." }),
      k("S"), k("nisha", { x: "Nisha Verma" }),
      k("0"),
    ],
  },
  {
    n: 4, file: "04-super-admin-user-details", step: 2, role: "Super Admin", task: "User details: edit, role, reset password, deactivate, Super Admin rules", video: "02-super-admin", user: "arya",
    small: "Edit, change role, reset password (new temporary password), deactivate + activate; a Super Admin and yourself are protected",
    items: [
      k("2"), k("S"), k("nisha"),
      k("1", { x: "Nisha Verma" }),
      k("1", { c: "SA.detail.1", x: "Edit User - USR-026" }), k(""), k(""), k("9811223355"), k("", { x: "Nisha Verma updated" }),
      k("2", { c: "SA.detail.2", x: "Edit User Role" }), k("Admin"), k("Moved to the platform team", { x: "Nisha Verma is now Admin" }),
      k("3", { c: "SA.detail.3" }), k("y", { p: 3, x: ["Password Reset", "Temporary password : Temp@"] }),
      k("4", { c: "SA.detail.4d", x: "Confirm Deactivation" }), k("y"), k("On leave until January", { x: ["Nisha Verma deactivated", "Activate user"] }),
      k("4", { c: "SA.detail.4a", x: "Confirm Activation" }), k("y", { x: "Nisha Verma activated" }),
      k("0"),
      k("C"),
      k("2", { x: "Rohan Kapoor" }),
      k("4", { c: "SA.rule.superadmin", err: true, x: "A Super Admin cannot be deactivated or removed" }),
      k("2", { c: "SA.rule.rolelocked", err: true }),
      k("1", { c: "SA.rule.superedit", x: "Edit User - USR-002" }), k(""), k(""), k("", { x: ["Role: Super Admin (a Super Admin role cannot be removed)", "Rohan Kapoor updated"] }),
      k("0"),
      k("1", { x: "Arya Mujumdar" }),
      k("4", { c: "SA.rule.self", err: true, x: "You cannot deactivate your own account" }),
      k("0"), k("0"),
    ],
  },
  {
    n: 5, file: "05-super-admin-audit-logs", step: 2, role: "Super Admin", task: "Audit logs with every filter, profile, change password", video: "02-super-admin", user: "arya",
    small: "Next / previous page, action + user filters, date range (with the From > To error), search, clear",
    items: [
      k("3", { c: "SA.3", x: "Audit Logs (newest first)" }),
      k("N", { c: "SA.audit.N", x: "Page 2 of" }), k("P", { c: "SA.audit.P", x: "Page 1 of" }),
      k("A", { c: "SA.audit.A" }), k("User Created", { x: "Action: User Created" }),
      k("U", { c: "SA.audit.U" }), k("Arya Mujumdar (USR-001)", { x: "Performed By: Arya Mujumdar" }),
      k("C", { c: "SA.audit.C" }),
      k("D", { c: "SA.audit.D.err" }), k("11/30/2026"), k("11/01/2026", { err: true, x: "From date must be before To date" }),
      k("D", { c: "SA.audit.D" }), k("11/01/2026"), k("-", { x: "From: 11/01/2026" }),
      k("S", { c: "SA.audit.S" }), k("Password", { x: "Search: Password" }),
      k("C"),
      k("X", { c: "SA.audit.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "SA.audit.0" }),
      k("4", { c: "SA.4", x: "Profile & Settings" }), k("0"),
      ...wrongPw("5", "SA.5"),
    ],
  },
  /* ------------------------------------------------------------------ 3. Cloud Admin */
  {
    n: 6, file: "06-cloud-admin-dashboard-companies", step: 3, role: "Cloud Admin", task: "Dashboard and companies", video: "03-cloud-admin", user: "aarav",
    small: "Dashboard, companies: search, risk filter, add company (errors + duplicate), paging, company detail",
    items: [
      k("1", { c: "CA.1", p: 3, x: "Governance Dashboard" }),
      k("2", { c: "CA.2", x: "Companies - 10 companies" }),
      k("S", { c: "CA.comp.S" }), k("contoso", { x: "Search: contoso" }),
      k("C", { c: "CA.comp.C" }),
      k("R", { c: "CA.comp.R" }), k("HIGH", { x: "Risk level: HIGH" }),
      k("C"),
      k("A", { c: "CA.comp.A.err", x: "Add Company Data" }), k("AB", { err: true, x: "Enter at least 3 characters" }), k("Margie Travel"),
      k("1234", { err: true, x: "Enter a valid tenant ID" }), k("8f2b6c1d-4e5a-4f3b-9c7d-2a1b3c4d5e6f", { c: "CA.comp.A" }), k("", { x: "Margie Travel added" }),
      k("A", { c: "CA.comp.A.dup" }), k("Northwind Traders"), k("9a8b7c6d-1111-2222-3333-444455556666"), k("2", { err: true, x: "This company already exists" }),
      k("N", { c: "CA.comp.N", x: "Page 2 of 2" }), k("P", { c: "CA.comp.P", x: "Page 1 of 2" }),
      k("1", { c: "CA.comp.row", x: "Azure Tenant ID" }),
      k("2", { c: "CA.comp.detail.2", x: "Identities - " }), k("0"),
      k("4", { x: "Tailspin Airlines" }), k("2", { c: "CA.comp.detail.2.err", err: true, x: "has no analysis yet" }),
      k("2", { x: "Contoso Retail" }), k("0", { c: "CA.comp.detail.0" }),
      k("Q", { c: "CA.comp.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "CA.comp.0" }),
      k("99", { c: "CA.wrongnum", err: true, x: "Type a number from 0 to 13." }),
    ],
  },
  {
    n: 7, file: "07-cloud-admin-wizard-errors", step: 3, role: "Cloud Admin", task: "Analysis Wizard: every input error", video: "03-cloud-admin", user: "aarav",
    small: "Bad period, missing file, not a .json file, broken JSON, missing fields, wrong JSON shape, cancel before saving",
    items: [
      k("3", { c: "CA.wiz.period.err", x: "Analysis Wizard - Step 1 of 4" }), k("Margie Travel"),
      k("4"), k("11/01/2026"), k("11/05/2026", { err: true, x: "Period must be at least 7 days" }),
      k("4"), k("08/12/2026"), k("12/31/2026", { err: true, x: "To date cannot be in the future" }),
      k("4"), k("08/12/2026"), k("11/09/2026", { x: "Period: 08/12/2026 - 11/09/2026" }),
      k(SAMPLES + "role-assignment.json", { c: "CA.wiz.err.missing", err: true, x: "file not found" }),
      k(SAMPLES + "README.md", { c: "CA.wiz.err.notjson", err: true, x: "please choose a .json file" }),
      k(SAMPLES + "role-assignments.json", { x: "Uploaded: role-assignments.json" }),
      k(SAMPLES + "bad\\activity-log-broken.json", { c: "CA.wiz.err.broken" }),
      k("", { err: true, p: 3, x: ["not valid JSON", "Nothing was saved"] }),
      k("3", { c: "CA.wiz.err.fields" }), k("Margie Travel"), k("4"), k("08/12/2026"), k("11/09/2026"),
      k(SAMPLES + "bad\\role-assignments-missing-fields.json"), k(""), k("", { err: true, p: 3, x: "has no principalId, scope" }),
      k("3", { c: "CA.wiz.err.shape" }), k("Margie Travel"), k("4"), k("08/12/2026"), k("11/09/2026"),
      k(SAMPLES + "bad\\role-assignments-wrong-shape.json"), k(""), k("", { err: true, p: 3, x: "it is a JSON object" }),
      k("3", { c: "CA.wiz.preset" }), k("Northwind Traders"), k("Last 90 days", { x: "90 days" }), k(""), k(""), k("", { x: "Validation passed" }),
      k("n", { c: "CA.wiz.cancel", x: "Cancelled. Nothing was saved." }),
    ],
  },
  {
    n: 8, file: "08-cloud-admin-wizard-run", step: 3, role: "Cloud Admin", task: "Analysis Wizard with the 3 JSON paths, results, previous analyses", video: "03-cloud-admin", user: "aarav",
    small: "Company > Run New Analysis: role assignments (typed path), activity log (full path), roles catalog (Enter = sample) - scores",
    items: [
      k("2"), k("N"), k("1", { x: "Margie Travel" }),
      k("1", { c: "CA.comp.detail.1", x: "Company: Margie Travel" }), k("4"), k("08/12/2026"), k("11/09/2026"),
      k(SAMPLES + "role-assignments.json", { c: "CA.wiz.paths", x: "Uploaded: role-assignments.json" }),
      k(PKG + "\\samples\\governance\\activity-log-90d.json", { x: "Uploaded: activity-log-90d.json" }),
      k("", { x: ["Uploaded: roles-catalog.json", "Validation passed"], p: 3 }),
      k("y", { c: "CA.wiz.run", p: 3, x: ["Step 4 of 4: Results", "Identities analysed"] }),
      k("0"),
      k("4", { c: "CA.4", x: "Previous Analyses" }),
      k("1", { p: 3, x: "Latest analysis complete - Margie Travel" }),
    ],
  },
  {
    n: 9, file: "09-cloud-admin-identities", step: 3, role: "Cloud Admin", task: "Identities and scores", video: "03-cloud-admin", user: "aarav",
    small: "Paging, search, type + risk filters, CSV export, other analysis, identity detail, generated role, evidence JSON",
    items: [
      k("5", { c: "CA.5", x: "Identities - " }),
      k("N", { c: "CA.id.N", x: "Page 2 of 2" }), k("P", { c: "CA.id.P", x: "Page 1 of 2" }),
      k("S", { c: "CA.id.S" }), k("svc", { x: "Search: svc" }),
      k("C", { c: "CA.id.C" }),
      k("T", { c: "CA.id.T" }), k("Service Principal", { x: "Type: Service Principal" }),
      k("R", { c: "CA.id.R" }), k("2", { x: "Risk: HIGH" }),
      k("C"),
      k("E", { c: "CA.id.E", x: "Saved 20 identities to" }),
      k("A", { c: "CA.id.A" }), k("2", { x: "of 128 identities" }),
      k("A"), k("1", { x: "Margie Travel" }),
      k("1", { c: "CA.id.row", p: 3, x: ["Why this score", "Permissions Used"] }),
      k("1", { c: "CA.id.detail.1", x: "Role definition (JSON)" }), k("0", { c: "CA.gr.back" }),
      k("1"), k("2", { c: "CA.id.detail.2", x: "-evidence.json" }),
      k("W", { c: "CA.id.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "CA.id.0" }),
    ],
  },
  {
    n: 10, file: "10-cloud-admin-findings", step: 3, role: "Cloud Admin", task: "High-risk findings with every filter", video: "03-cloud-admin", user: "aarav",
    small: "Category, severity and identity filters, paging, CSV export, other analysis, open the identity",
    items: [
      k("6", { c: "CA.6", x: "High-Risk Findings - " }),
      k("N", { c: "CA.fd.N" }), k("P", { c: "CA.fd.P" }),
      k("K", { c: "CA.fd.K" }), k("Delete", { x: "Category: Delete" }),
      k("V", { c: "CA.fd.V" }), k("HIGH", { x: "Severity: HIGH" }),
      k("C", { c: "CA.fd.C" }),
      k("I", { c: "CA.fd.I" }), k("2"),
      k("C"),
      k("E", { c: "CA.fd.E", x: "findings to" }),
      k("A", { c: "CA.fd.A" }), k("2", { x: "AN-2026-1110" }),
      k("1", { c: "CA.fd.row", x: "Why this score" }), k("0"),
      k("X", { c: "CA.fd.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "CA.fd.0" }),
    ],
  },
  {
    n: 11, file: "11-cloud-admin-generated-roles", step: 3, role: "Cloud Admin", task: "Generated least-privilege roles", video: "03-cloud-admin", user: "aarav",
    small: "Role list, role JSON, mark reviewed, baseline read actions on / off, save the JSON file, other analysis",
    items: [
      k("7", { c: "CA.7", x: "Generated Roles - " }),
      k("N", { c: "CA.gr.N" }), k("P", { c: "CA.gr.P" }),
      k("1", { c: "CA.gr.row", p: 3, x: "Role definition (JSON)" }),
      k("1", { c: "CA.gr.1", x: "marked as reviewed" }),
      k("2", { c: "CA.gr.2", x: "Saved." }), k("2", { x: "Saved." }),
      k("3", { c: "CA.gr.3", x: ".json" }),
      k("0"),
      k("A", { c: "CA.gr.A" }), k("2", { x: "AN-2026-1110" }),
      k("Q", { c: "CA.gr.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "CA.gr.0" }),
    ],
  },
  {
    n: 12, file: "12-cloud-admin-rules-patterns", step: 3, role: "Cloud Admin", task: "Rule settings and high-risk patterns", video: "03-cloud-admin", user: "aarav",
    small: "rules.json, weights must total 100%, inline errors, reset to default; add / duplicate / disable / enable / delete a pattern",
    items: [
      k("8", { c: "CA.8", x: "Rule Settings (rules.json)" }),
      k("2", { c: "CA.rules.2", x: "rules.json (current values)" }),
      k("1", { c: "CA.rules.1.err" }), k("50"), k("30"), k("30", { x: "Total: 110%" }), k(""), k(""), k("", { err: true, x: "Weights must add up to 100%" }),
      k("1", { c: "CA.rules.1" }), k("101", { err: true, x: "Whole number from 0 to 100" }), k("40"), k("40"), k("20", { x: "Total: 100%" }),
      k("60 days"), k("60", { err: true, x: "Rare action threshold must be 1 to 50" }), k("5"), k("No", { x: "rules.json saved" }),
      k("3", { c: "CA.rules.3" }), k("y", { x: "Rules reset to default" }),
      k("7", { c: "CA.rules.wrong", err: true, x: "Type 1, 2, 3 or 0." }),
      k("0", { c: "CA.rules.0" }),
      k("9", { c: "CA.9", x: "High-Risk Patterns" }),
      k("A", { c: "CA.pat.A.err" }), k("Microsoft.Compute/$all", { err: true, x: "Only letters, numbers" }),
      k("Microsoft.Sql/servers/*/delete", { c: "CA.pat.A", x: "Pattern added" }),
      k("A", { c: "CA.pat.A.dup" }), k("Microsoft.Sql/servers/*/delete", { err: true, x: "This pattern already exists" }), k("cancel", { x: "Cancelled" }),
      k("5", { c: "CA.pat.row.1", x: "Disable Microsoft.Sql/servers/*/delete" }), k("1", { x: "Pattern disabled" }),
      k("5"), k("1", { x: "Pattern enabled" }),
      k("5", { c: "CA.pat.row.2" }), k("2"), k("y", { x: "Pattern removed" }),
      k("Z", { c: "CA.pat.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "CA.pat.0" }),
    ],
  },
  {
    n: 13, file: "13-cloud-admin-reports-notifications", step: 3, role: "Cloud Admin", task: "Reports, notifications, profile and activity", video: "03-cloud-admin", user: "aarav",
    small: "Generate a report (and the error without an analysis), preview + HTML file, mark notifications read, my recent activity",
    items: [
      k("10", { c: "CA.10", x: "Reports" }),
      k("G", { c: "CA.rep.G" }), k("Margie Travel", { x: "generated for Margie Travel" }),
      k("G", { c: "CA.rep.G.err" }), k("Tailspin Airlines", { err: true, x: "has no analysis yet" }),
      k("1", { c: "CA.rep.row", x: "Report Preview" }), k("y", { x: ".html" }),
      k("X", { c: "CA.rep.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "CA.rep.0" }),
      k("11", { c: "CA.11", x: "Notifications" }), k("y", { x: "All notifications marked as read" }),
      k("12", { c: "CA.12", x: "Profile & Settings" }), k("3", { x: "My recent activity" }), k("0"),
      ...wrongPw("13", "CA.13"),
    ],
  },
  /* ------------------------------------------------------------------ 4. Admin */
  {
    n: 14, file: "14-admin-dashboard-enterprises", step: 4, role: "Admin", task: "Dashboard, register an enterprise, choose the enterprise, edit registration", video: "04-admin", user: "sachin",
    small: "Search, category filter, paging, enterprise details; registration errors; current enterprise; edit registration",
    items: [
      k("1", { c: "AD.1", x: "Admin Dashboard - Enterprises" }),
      k("S", { c: "AD.dash.S" }), k("contoso", { x: "Search: contoso" }),
      k("C", { c: "AD.dash.C" }),
      k("K", { c: "AD.dash.K" }), k("Pending", { x: "Category: Pending" }),
      k("C"),
      k("N", { c: "AD.dash.N", x: "Page 1 of 1" }), k("P"),
      k("1", { c: "AD.dash.row", x: "Northwind Bank (ENT-001)" }), k("0", { c: "AD.4.0" }),
      k("Q", { c: "AD.dash.unknown", err: true, x: "Unknown choice" }),
      k("R", { c: "AD.dash.R", x: "Register New Enterprise" }), k("cancel", { x: "Cancelled" }),
      k("0", { c: "AD.dash.0" }),
      k("2", { c: "AD.2.err", x: "Register New Enterprise" }),
      k("Northwind Bank", { err: true, x: "already registered" }), k("Tailspin Airlines"),
      k("", { err: true, x: "Select an industry" }), k("Other"),
      k("Pune1", { err: true, x: "Letters only" }), k("Pune"),
      k("Kavita Desai"),
      k("kavita.desai@tailspin", { err: true, x: "Enter a valid email" }), k("kavita.desai@tailspin.com"),
      k("5812300011", { err: true, x: "10 digits starting with 6, 7, 8 or 9" }), k("9812300011", { c: "AD.2", p: 3, x: "Tailspin Airlines registered as ENT-004" }),
      k("3", { c: "AD.3" }), k("1", { x: "Current enterprise: Northwind Bank (ENT-001)" }),
      k("3"), k("4", { x: "Current enterprise: Tailspin Airlines (ENT-004)" }),
      k("4", { c: "AD.4", x: "Pending Classification" }),
      k("1", { c: "AD.4.1", x: "Edit Registration" }), k("Mumbai"), k(""), k(""), k("", { x: "Registration details saved" }),
      k("11", { c: "AD.wrongnum", err: true, x: "Type a number from 0 to 10." }),
    ],
  },
  {
    n: 15, file: "15-admin-emsi-classification", step: 4, role: "Admin", task: "EMSI classification", video: "04-admin", user: "sachin",
    small: "12 parameters with inline errors, levels must match the total, fix and calculate: EMSI, category and plan price",
    items: [
      k("3"), k("4", { x: "Current enterprise: Tailspin Airlines" }),
      k("5", { c: "AD.5.err", x: "Portfolio Details - Tailspin Airlines" }),
      k("12"), k("20"), k("8"),
      k("500", { err: true, x: "Whole number from 1,000" }), k("250000"),
      k("3"), k("400"),
      k("25", { err: true, x: "Whole number from 1 to 20" }), k("2"),
      k("Public Cloud"), k("2"), k("30"),
      k("", { err: true, x: "Select at least one technology" }), k("1,3"),
      k("2,4"),
      k("3"), k("4"), k("3"), k("5", { err: true, p: 3, x: ["(Must match)", "EMSI not calculated"] }),
      k("y"),
      ...Array.from({ length: 7 }, () => k("")),
      k(""), k(""), k(""), k(""), k(""),
      k(""), k(""), k(""),
      k("2", { c: "AD.5", p: 3, x: ["(Matches)", "EMSI 32.33 - Growth category", "Growth Subscription Plan"] }),
      k("4", { x: "Classified" }),
      k("2", { c: "AD.4.2", x: "Portfolio Details" }), k("cancel", { x: "Cancelled. Nothing was saved." }),
    ],
  },
  {
    n: 16, file: "16-admin-applications", step: 4, role: "Admin", task: "Application inventory: register, filters, edit, delete", video: "04-admin", user: "sachin",
    small: "Register 2 applications (with field errors and a duplicate), search + filters, view, edit, delete",
    items: [
      k("3"), k("4", { x: "Current enterprise: Tailspin Airlines" }),
      k("6", { c: "AD.6", x: "Application Inventory - Tailspin Airlines" }),
      k("R", { c: "AD.app.R.err", x: "Register Application" }),
      k("FB", { err: true }), k("Flight Booking"), k("Sales"), k("Web Team"), k("Active"), k("Public Cloud"), k("Java"),
      k("Java", { err: true, x: "Name + version number" }), k("Java 17"), k("Supported"),
      k("60", { err: true, x: "Whole number from 0 to 50" }), k("6"), k("Good"), k("Medium"),
      k("50", { err: true, x: "Whole number, at least 100" }), k("300000"), k("Critical"), k("Yes"), k("1"), k("Low"),
      k("1", { c: "AD.app.R", p: 3, x: "Flight Booking registered as APP-011" }),
      k("R"), k("Crew Scheduler"), k("Operations"), k("Ops Team"), k("Legacy"), k("On-Premise"), k(".NET"), k(".NET 4.8"), k("Unsupported"),
      k("15"), k("Poor"), k("High"), k("800000"), k("High"), k("No"), k("4"), k("Medium"), k("2", { x: "Crew Scheduler registered as APP-012" }),
      k("R", { c: "AD.app.R.dup" }), k("Flight Booking", { err: true, x: "This application already exists in Tailspin Airlines" }), k("cancel", { x: "Cancelled" }),
      k("S", { c: "AD.app.S" }), k("flight", { x: "Search: flight" }), k("C", { c: "AD.app.C" }),
      k("T", { c: "AD.app.T" }), k("Java", { x: "Technology: Java" }),
      k("K", { c: "AD.app.K" }), k("Critical", { x: "Criticality: Critical" }),
      k("A", { c: "AD.app.A" }), k("Not Assessed", { x: "Assessment: Not Assessed" }),
      k("C"),
      k("N", { c: "AD.app.N", x: "Page 1 of 1" }), k("P"),
      k("2", { c: "AD.app.row", x: "Crew Scheduler (APP-012)" }), k("0", { c: "AD.app.row.0" }),
      k("1", { x: "Flight Booking (APP-011)" }),
      k("1", { c: "AD.app.row.1", x: "Edit Application - APP-011" }),
      ...Array.from({ length: 14 }, () => k("")), k("2"), k(""), k(""),
      k("y", { x: "Flight Booking updated" }),
      k("Q", { c: "AD.app.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "AD.app.0" }),
      k("3"), k("2", { x: "Contoso Retail" }),
      k("6"), k("4", { x: "Supplier Portal" }),
      k("2", { c: "AD.app.row.2" }), k("y", { x: "Supplier Portal deleted" }),
      k("0"),
      k("3"), k("4", { x: "Tailspin Airlines" }),
    ],
  },
  {
    n: 17, file: "17-admin-components-dependencies", step: 4, role: "Admin", task: "Components and dependencies, with every error", video: "04-admin", user: "sachin",
    small: "Add components, add links (duplicate, self, own parent refused), edit + delete a link, delete a component, impact",
    items: [
      k("3"), k("4", { x: "Current enterprise: Tailspin Airlines" }),
      k("7", { c: "AD.7", x: "Components & Dependencies - Tailspin Airlines" }),
      k("A", { c: "AD.dep.A", x: "Add Component" }), k("", { err: true, x: "Select a type" }), k("Database"), k("1"), k("Booking Database", { x: "Database Booking Database added" }),
      k("A"), k("API"), k("1"), k("Booking API", { x: "API Booking API added" }),
      k("A"), k("Service"), k("2"), k("Roster Service", { x: "Service Roster Service added" }),
      k("L", { c: "AD.dep.L", x: "Add Dependency" }), k("1"), k("2"), k("Critical"), k("Stores every booking", { x: "Dependency DEP-021 saved" }),
      k("L"), k("4"), k("1"), k("High"), k("Reads the flight times", { x: "Dependency DEP-022 saved" }),
      k("L", { c: "AD.dep.L.dup" }), k("1"), k("2"), k("Critical"), k("", { err: true, x: "This link already exists" }), k("n"),
      k("L", { c: "AD.dep.L.self" }), k("1"), k("1"), k("Low"), k("", { err: true, x: "Source and target cannot be the same" }),
      k("y", { c: "AD.dep.L.parent" }), k("3"), k("1"), k("Low"), k("", { err: true, x: "cannot depend on its own parent application" }),
      k("y"), k("5"), k("3"), k("Medium"), k("Publishes the crew roster", { x: "Dependency DEP-023 saved" }),
      k("E", { c: "AD.dep.E" }), k("2"), k(""), k(""), k("Critical"), k("", { x: "Dependency DEP-022 updated" }),
      k("D", { c: "AD.dep.D" }), k("3"), k("y", { x: "Dependency deleted" }),
      k("X", { c: "AD.dep.X" }), k("3"), k("y", { x: "Component deleted" }),
      k("I", { c: "AD.dep.I", p: 3, x: "Dependency Impact (outgoing links of each application)" }),
      k("S", { c: "AD.dep.S" }), k("booking", { x: "Search: booking" }),
      k("F", { c: "AD.dep.F" }), k("Database"), k("All", { x: "Type: Database" }),
      k("C", { c: "AD.dep.C" }),
      k("Q", { c: "AD.dep.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "AD.dep.0" }),
    ],
  },
  {
    n: 18, file: "18-admin-digital-twin", step: 4, role: "Admin", task: "Digital twin, profile, change password", video: "04-admin", user: "sachin",
    small: "Text view of applications, components and links: one application, criticality filter, another enterprise",
    items: [
      k("3"), k("4", { x: "Current enterprise: Tailspin Airlines" }),
      k("8", { c: "AD.8", p: 3, x: "Digital Twin - Tailspin Airlines" }),
      k("A", { c: "AD.twin.A" }), k("1", { x: "application: Flight Booking" }),
      k("K", { c: "AD.twin.K" }), k("Critical", { x: "criticality: Critical" }),
      k("C", { c: "AD.twin.C" }),
      k("E", { c: "AD.twin.E" }), k("1", { p: 3, x: "Digital Twin - Northwind Bank" }),
      k("Z", { c: "AD.twin.unknown", err: true, x: "Type A, K, C, E or 0." }),
      k("0", { c: "AD.twin.0" }),
      k("9", { c: "AD.9", x: "Profile & Settings" }), k("0"),
      ...wrongPw("10", "AD.10"),
    ],
  },
  /* ------------------------------------------------------------------ 5. Architect */
  {
    n: 19, file: "19-architect-dashboard-applications", step: 5, role: "Architect", task: "Dashboard, current enterprise, applications, first assessment", video: "05-architect", user: "aman",
    small: "Dashboard, choose Tailspin Airlines, read-only application list with filters, assess Flight Booking",
    items: [
      k("1", { c: "AR.1", p: 3, x: "Architect Dashboard" }),
      k("2", { c: "AR.2" }), k("4", { x: "Current enterprise: Tailspin Airlines" }),
      k("3", { c: "AR.3", x: "Applications - Tailspin Airlines" }),
      k("S", { c: "AR.app.S" }), k("crew", { x: "Search: crew" }), k("C", { c: "AR.app.C" }),
      k("A", { c: "AR.app.A" }), k("Not Assessed"), k("C"),
      k("N", { c: "AR.app.N", x: "Page 1 of 1" }), k("P"),
      k("Q", { c: "AR.app.unknown", err: true, x: "Unknown choice" }),
      k("1", { c: "AR.app.row", x: "Flight Booking (APP-011)" }),
      k("1", { c: "AR.app.row.1", x: "Assessment - Flight Booking" }),
      k("1", { c: "AR.asm.1", p: 3, x: ["Assessment ASM-006 saved", "Technical Debt", "Dependency Impact"] }),
      k("0", { c: "AR.asm.0" }),
      k("0", { c: "AR.app.0" }),
      k("11", { c: "AR.wrongnum", err: true, x: "Type a number from 0 to 10." }),
    ],
  },
  {
    n: 20, file: "20-architect-assessments", step: 5, role: "Architect", task: "Assessments: not assessed yet, run, recalculate", video: "05-architect", user: "aman",
    small: "Crew Scheduler: simulation is refused before the assessment; run it, recalculate it, history",
    items: [
      k("4", { c: "AR.4" }), k("11", { x: "Assessment - Crew Scheduler" }),
      k("2", { c: "AR.asm.2.err", err: true, x: "Run the assessment first" }),
      k("1", { x: "Assessment ASM-007 saved" }),
      k("1", { c: "AR.asm.1r", p: 3, x: ["Assessment ASM-008 saved", "History:"] }),
      k("2", { c: "AR.asm.2", x: "Modernization strategy" }), k("cancel", { x: "Cancelled. Nothing was saved." }),
    ],
  },
  {
    n: 21, file: "21-architect-simulations", step: 5, role: "Architect", task: "Simulations for every strategy", video: "05-architect", user: "aman",
    small: "Flight Booking with Rehost, Replatform, Refactor (default) and Rebuild; discard one and run again; open a saved one",
    items: [
      k("5", { c: "AR.5", x: "Simulations" }),
      k("R", { c: "AR.sim.rehost" }), k("6"), k("1", { p: 3, x: ["Simulation SIM-007 complete", "Rehost"] }), k("0", { c: "AR.sim.res.0" }),
      k("R", { c: "AR.sim.replatform" }), k("6"), k("2", { p: 3, x: ["Simulation SIM-008 complete", "Replatform"] }), k("0"),
      k("R", { c: "AR.sim.refactor" }), k("6"), k("", { p: 3, x: ["Simulation SIM-009 complete", "Refactor"] }), k("0"),
      k("R", { c: "AR.sim.rebuild" }), k("6"), k("4", { p: 3, x: ["Simulation SIM-010 complete", "Rebuild"] }),
      k("2", { c: "AR.sim.discard", x: "Simulation discarded" }),
      k("R"), k("6"), k("4", { x: "Simulation SIM-011 complete" }), k("0"),
      k("4", { c: "AR.sim.row", x: "SIM-007" }), k("0"),
      k("Q", { c: "AR.sim.unknown", err: true, x: "Type R, a row number or 0." }),
      k("0", { c: "AR.sim.0" }),
    ],
  },
  {
    n: 22, file: "22-architect-create-plan", step: 5, role: "Architect", task: "Create plans: phases 15/20/45/rest, draft, submit", video: "05-architect", user: "aman",
    small: "Phase weeks come from the effort (rounding goes to the last phase); name check; save as draft; submit (no / yes); cancel",
    items: [
      k("5", { x: "Simulations" }), k("2", { x: "SIM-009" }),
      k("1", { c: ["AR.sim.res.1", "AR.6"], p: 3, x: ["Create Plan - from SIM-009", "Total estimated effort:"] }),
      k("HR System Rebuild Plan", { c: "AR.plan.name.err", err: true, x: "A plan with this name already exists" }),
      k(""), k("Phase 1 starts in January"),
      k("1", { c: "AR.plan.1", x: "PLAN-007 saved as draft" }),
      k("0"),
      k("6", { x: "Create Modernization Plan - choose a simulation" }), k("1", { p: 3, x: "Create Plan - from SIM-011" }), k(""), k("Full rewrite on a modern stack"),
      k("2", { c: "AR.plan.2n", x: "After submission, this plan will be locked" }), k("n", { x: "Not submitted. Nothing was saved." }),
      k("6"), k("1"), k(""), k("Full rewrite on a modern stack"),
      k("2", { c: "AR.plan.2" }), k("y", { p: 3, x: "PLAN-008 submitted for approval" }),
      k("6"), k("1", { x: "Create Plan - from SIM-008" }), k(""), k(""),
      k("0", { c: "AR.plan.0", x: "Cancelled. Nothing was saved." }),
    ],
  },
  {
    n: 23, file: "23-architect-my-plans", step: 5, role: "Architect", task: "My plans: edit the draft, submit, read-only plans", video: "05-architect", user: "aman",
    small: "Status filter, search, edit a draft, submit it (locked), a submitted plan is read-only, a simulation with a plan cannot be discarded",
    items: [
      k("7", { c: "AR.7", x: "My Plans" }),
      k("T", { c: "AR.my.T" }), k("Draft", { x: "Status: Draft" }),
      k("S", { c: "AR.my.S" }), k("flight", { x: "Search: flight" }),
      k("C", { c: "AR.my.C" }),
      k("2", { c: "AR.my.draft.1", x: "Draft plan - you can edit" }), k("1"), k("Flight Booking Refactor Plan v2"), k("Phase 1 starts in February", { x: "PLAN-007 saved as draft" }),
      k("2", { c: "AR.my.draft.2" }), k("2"), k(""), k(""), k("y", { p: 3, x: "PLAN-007 submitted for approval" }),
      k("1", { c: "AR.my.locked", x: "This plan is read-only." }),
      k("Z", { c: "AR.my.unknown", err: true, x: "Unknown choice" }),
      k("0", { c: "AR.my.0" }),
      k("5"), k("2", { c: "AR.sim.discard.err" }), k("2", { err: true, x: "already has a plan" }),
      k("2", { c: "AR.sim.res.open" }), k("1", { x: "This plan is read-only." }),
      k("0"),
      k("2"), k("4", { x: "Current enterprise: Tailspin Airlines" }),
      k("3"), k("1"), k("2", { c: "AR.app.row.2", x: "Simulation - Flight Booking" }), k("1", { x: "Simulation SIM-012 complete" }), k("0"), k("0"),
      k("8", { c: "AR.8", x: "Digital Twin - Tailspin Airlines" }), k("0"),
      k("9", { c: "AR.9", x: "Profile & Settings" }), k("0"),
      ...wrongPw("10", "AR.10"),
    ],
  },
  /* ------------------------------------------------------------------ 6. Executive */
  {
    n: 24, file: "24-executive-review-approve-reject", step: 6, role: "Executive", task: "Review, approve one plan, reject another", video: "06-executive", user: "shiva",
    small: "Filters on the pending list, the full review screen, approve with a comment, reject (a reason is required), cancel a decision",
    items: [
      k("1", { c: "EX.1", x: "Executive Dashboard - Modernization Plans" }),
      k("R", { c: "EX.list.R" }), k("High", { x: "High risk" }),
      k("C", { c: "EX.list.C" }),
      k("E", { c: "EX.list.E" }), k("Tailspin Airlines (ENT-004)", { x: "Tailspin Airlines" }),
      k("T", { c: "EX.list.T" }), k("All", { x: "Showing: All plans" }),
      k("C"),
      k("N", { c: "EX.list.N", x: "Page 1 of 1" }), k("P"),
      k("Q", { c: "EX.list.unknown", err: true, x: "Unknown choice" }),
      k("1", { c: "EX.review", p: 3, x: ["PLAN-008", "Phases:", "Architect notes"] }),
      k("1", { c: "EX.review.1" }), k("Approved for the Q1 2027 budget"), k("y", { p: 3, x: "PLAN-008 approved" }),
      k("1", { x: "PLAN-007" }),
      k("2", { c: "EX.review.2.err" }), k("", { err: true, x: "Please give a reason (at least 5 characters) to reject" }),
      k("No", { err: true, x: "Please give a reason" }),
      k("Too costly this year - revisit in Q3", { c: "EX.review.2" }), k("y", { p: 3, x: "PLAN-007 rejected" }),
      k("1", { c: "EX.review.n" }), k("1"), k(""), k("n", { x: "No decision was saved." }),
      k("1"), k("0", { c: "EX.review.0" }),
      k("0", { c: "EX.list.0" }),
    ],
  },
  {
    n: 25, file: "25-executive-all-plans-history", step: 6, role: "Executive", task: "All plans with filters, decided plans, decision history", video: "06-executive", user: "shiva",
    small: "Status / risk / enterprise filters, an approved plan cannot be decided again, decision history",
    items: [
      k("2", { c: "EX.2", x: "Showing: All plans" }),
      k("T"), k("Approved", { x: "Showing: Approved plans" }),
      k("1", { c: "EX.decided", x: "Executive decision: Approved" }),
      k("T"), k("Rejected", { x: "Showing: Rejected plans" }),
      k("T"), k("All"), k("R"), k("Medium"), k("E"), k("Northwind Bank (ENT-001)", { x: "Northwind Bank" }),
      k("C"), k("0"),
      k("3", { c: "EX.3", p: 3, x: "Decision history" }),
      k("6", { c: "EX.wrongnum", err: true, x: "Type a number from 0 to 5." }),
    ],
  },
  /* ------------------------------------------------------------------ 7. live database change */
  {
    n: 26, file: "26-live-db-change-users", step: 7, role: "Live DB change", task: "psql changes users while the app is open", video: "07-live-db-change",
    small: "Every screen reads fresh from the database: a renamed user shows at once, a deactivated user is logged out at the next click",
    items: [
      sh("# A teammate changes the database with psql -f while SecureShift is open (every screen reads fresh)", { p: 1 }),
      sh("function Teammate($file, $seconds) { Start-Process -NoNewWindow powershell -ArgumentList '-NoProfile', '-Command', \"Start-Sleep $seconds; & '$psql' -h 127.0.0.1 -p 5434 -U postgres -d secureshift -e -f $file; Write-Host -NoNewline '(psql done) '\" }", { p: 1 }),
      sh("Get-Content " + LIVE + "\\01-rename-user.sql", { file: "01-rename-user.sql", p: 2 }),
      sh("Teammate " + LIVE + "\\01-rename-user.sql", { teammate: "01-rename-user.sql", p: 0.5 }),
      sh(APP, { app: true }),
      ...login(...USERS.arya),
      k("2", { c: "live.users", x: "Tejas Pathak" }),
      k("C", { teammate: "01-rename-user.sql", p: 3, x: "Tejas Pathak Live" }),
      k("0"), k("0"), k("y"), k("0"),
      sh("Get-Content " + LIVE + "\\02-deactivate-admin.sql", { file: "02-deactivate-admin.sql", p: 2 }),
      sh("Teammate " + LIVE + "\\02-deactivate-admin.sql", { teammate: "02-deactivate-admin.sql", p: 0.5 }),
      sh(APP, { app: true }),
      ...login(...USERS.sachin),
      k("1", { c: "session.deactivated", teammate: "02-deactivate-admin.sql", p: 3, x: "no longer active" }),
      k("1"), k(USERS.sachin[0]), k(USERS.sachin[1], { pw: true, err: true, x: "Your account is inactive" }),
      k("0"),
      sh("& $psql -h 127.0.0.1 -p 5434 -U postgres -d secureshift -e -f " + LIVE + "\\03-reactivate-admin.sql", { psql: "03-reactivate-admin.sql", p: 2 }),
    ],
  },
  {
    n: 27, file: "27-live-db-change-plans", step: 7, role: "Live DB change", task: "Legacy password hash, a plan decided by someone else", video: "07-live-db-change",
    small: "An old SHA-256 password hash is upgraded at login; a plan approved in psql while the review is open cannot be rejected",
    items: [
      sh("# 1. An old (SHA-256) password hash, like the original SQL Server seed data", { p: 1 }),
      sh("Get-Content " + LIVE + "\\04-legacy-hash.sql", { file: "04-legacy-hash.sql", p: 2 }),
      sh("& $psql -h 127.0.0.1 -p 5434 -U postgres -d secureshift -e -f " + LIVE + "\\04-legacy-hash.sql", { psql: "04-legacy-hash.sql", p: 2 }),
      sh("# 2. Another executive approves PLAN-001 in psql while this review screen is open", { p: 1 }),
      sh("function Teammate($file, $seconds) { Start-Process -NoNewWindow powershell -ArgumentList '-NoProfile', '-Command', \"Start-Sleep $seconds; & '$psql' -h 127.0.0.1 -p 5434 -U postgres -d secureshift -e -f $file; Write-Host -NoNewline '(psql done) '\" }", { p: 1 }),
      sh("Get-Content " + LIVE + "\\05-approve-plan.sql", { file: "05-approve-plan.sql", p: 2 }),
      sh("Teammate " + LIVE + "\\05-approve-plan.sql", { teammate: "05-approve-plan.sql", p: 0.5 }),
      sh(APP, { app: true }),
      ...login(...USERS.shiva, { c: "login.legacy", p: 3, x: ["stored in an old format", "Signed in as"] }),
      k("1"), k("1", { x: "PLAN-001" }),
      k("2", { c: "EX.race", teammate: "05-approve-plan.sql" }), k("Budget is frozen"),
      k("y", { err: true, p: 3, x: "PLAN-001 was already approved. The decision cannot be changed." }),
      k("0"), k("0"), k("y"), k("0"),
      sh("& $psql -h 127.0.0.1 -p 5434 -U postgres -d secureshift -f " + LIVE + "\\06-show-plans.sql", { psql: "06-show-plans.sql", p: 3 }),
    ],
  },
  /* ------------------------------------------------------------------ 8. profile, change password, exit */
  {
    n: 28, file: "28-executive-profile-password-exit", step: 8, role: "Executive", task: "Profile, change password, logout, exit", video: "06-executive", user: "shiva",
    small: "Edit details (phone check), change password in the profile (errors first), change it back from the menu, logout, exit",
    items: [
      k("4", { c: "EX.4", x: "Profile & Settings" }),
      k("1", { c: "EX.prof.1.err", x: "Profile Details" }), k(""), k("12345", { err: true, x: "Exactly 10 digits" }), k("9822012345"), k("Executive Office", { c: "EX.prof.1", x: "Profile updated" }),
      k("2", { c: "EX.prof.2.err" }), k("Exec@2025", { pw: true }), k("short", { pw: true }), k("short", { pw: true, err: true, x: ["Current password is wrong", "Min 8 characters"] }),
      k("2", { c: "EX.prof.2" }), k("Exec@2026", { pw: true }), k("Board@2027", { pw: true }), k("Board@2027", { pw: true, x: "Password updated" }),
      k("0", { c: "EX.prof.0" }),
      k("5", { c: "EX.5" }), k("Board@2027", { pw: true }), k("Exec@2026", { pw: true }), k("Exec@2026", { pw: true, x: "Password updated" }),
      k("0", { c: "login.logout.no" }), k("n"),
      k("0"), k("y", { x: "You have been logged out" }),
      k("1"), k(USERS.shiva[0]), k("Exec@2026", { pw: true, x: "Signed in as Shivanand Kumatekar" }),
      k("0"), k("y"),
      k("0", { c: "login.exit", p: 3, x: "Goodbye." }),
    ],
    noLogout: true,
  },
];

// clips that log in at the start and log out + exit at the end
for (const c of CLIPS) {
  if (c.shell) continue;
  const head = [], tail = [];
  if (!c.items.some(i => i.app)) head.push(sh(APP, { app: true }));
  if (c.user) head.push(...login(...USERS[c.user]));
  if (c.user && !c.noLogout) tail.push(...logoutExit());
  c.items = [...head, ...c.items, ...tail];
}

/* =====================================================================================================
 * 3. Coverage
 * ===================================================================================================== */
function coverageRows() {
  const rows = [];
  for (const clip of CLIPS) {
    let current = null;
    const flat = [];
    for (const it of clip.items) { flat.push(it); if (it.keys) flat.push(...it.keys.map(x => ({ ...x, inShell: true }))); }
    flat.forEach((it, idx) => {
      const ids = it.c ? [].concat(it.c) : [];
      if (ids.length) {
        current = { clip, ids, idx, typed: [] };
        rows.push(current);
      }
      if (current) {
        if (it.t === "key") current.typed.push(it.pw ? (it.text === "" ? "⏎" : "*".repeat(it.text.length)) : (it.text === "" ? "⏎" : it.text));
        else current.typed.push(it.text.length > 70 ? it.text.slice(0, 67) + "..." : it.text);
      }
    });
  }
  return rows;
}

function checkCoverage(print) {
  const rows = coverageRows();
  const covered = new Set(rows.flatMap(r => r.ids));
  const unknown = [...covered].filter(id => !OPT.has(id));
  const missing = OPTIONS.map(o => o[0]).filter(id => !covered.has(id));
  if (print) {
    console.log(`coverage: ${OPTIONS.length - missing.length} / ${OPTIONS.length} menu options and error cases`);
    if (missing.length) console.log("NOT covered: " + missing.join(", "));
    if (unknown.length) console.log("unknown ids in the clips: " + unknown.join(", "));
  }
  return { rows, missing, unknown };
}

/* =====================================================================================================
 * 4. Dry run: the same keys, against the real app + database, without VHS
 * ===================================================================================================== */
function arg(name, def) { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : def; }
function readConn() {
  const t = fs.readFileSync(path.join(TOOLS, "connection-string.txt"), "utf8").trim();
  const get = k => (t.match(new RegExp(k + "=([^;]*)", "i")) || [])[1];
  return { text: t, host: get("Host"), port: get("Port"), db: get("Database"), user: get("Username"), password: get("Password") };
}
const rowsOf = line => Math.max(1, Math.ceil([...line].length / COLS));
const sleep = ms => new Promise(r => setTimeout(r, ms));

function runPsql(conn, sqlArgs) {
  const r = cp.spawnSync(path.join(TOOLS, "pgsql", "bin", "psql.exe"), ["-h", "127.0.0.1", "-p", conn.port, "-U", "postgres", "-d", "secureshift", ...sqlArgs],
    { env: { ...process.env, PGPASSWORD: conn.password }, encoding: "utf8" });
  if (r.status !== 0) throw new Error("psql failed: " + r.stderr);
  return (r.stdout || "").replace(/\r/g, "");
}

/** pg_dump / pg_restore of the schema dev (to rerun the dry run from any clip). */
function snapshot(mode, file) {
  const conn = readConn();
  const bin = path.join(TOOLS, "pgsql", "bin");
  const env = { ...process.env, PGPASSWORD: conn.password };
  const base = ["-h", "127.0.0.1", "-p", conn.port, "-U", "postgres", "-d", "secureshift"];
  const r = mode === "save"
    ? cp.spawnSync(path.join(bin, "pg_dump.exe"), [...base, "-Fc", "-n", "dev", "-f", file], { env, encoding: "utf8" })
    : cp.spawnSync(path.join(bin, "pg_restore.exe"), [...base, "--clean", "--if-exists", "--no-owner", file], { env, encoding: "utf8" });
  if (r.status !== 0) throw new Error(`${mode} snapshot ${file} failed: ${r.stderr}`);
}

/** Clip 00: run the shell commands for real in one PowerShell, with markers between them. */
function dryRunShell(clip) {
  const script = path.join(DEMO, "work", "dryrun-00.ps1");
  fs.mkdirSync(path.dirname(script), { recursive: true });
  const lines = ["$ErrorActionPreference = 'Continue'", "$ProgressPreference = 'SilentlyContinue'", `Set-Location ${clip.cwd}`];
  clip.items.forEach((it, i) => {
    lines.push(`Write-Output '<<<CMD ${i}>>>'`);
    if (it.text.startsWith("#")) return;
    lines.push(it.keys ? `'${it.keys.map(x => x.text).join("','")}' | ${it.text}` : it.text);
  });
  lines.push("Write-Output '<<<END>>>'");
  fs.writeFileSync(script, lines.join("\r\n") + "\r\n");
  const r = cp.spawnSync("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script], { encoding: "utf8", maxBuffer: 64 << 20, timeout: 1800e3 });
  const out = (r.stdout || "") + (r.stderr ? "\n[stderr]\n" + r.stderr : "");
  const parts = out.replace(/\r/g, "").split(/<<<CMD (\d+)>>>\n/);
  const layout = [], transcript = [];
  const failures = [];
  for (let i = 0; i < clip.items.length; i++) {
    const idx = parts.indexOf(String(i));
    let text = idx >= 0 ? parts[idx + 1] : "";
    text = text.split("<<<END>>>")[0].replace(/\n+$/, "");
    const lines2 = text.length ? text.split("\n") : [];
    const cmdLine = "> " + clip.items[i].text;
    transcript.push(cmdLine, ...lines2);
    layout.push({ rows: rowsOf(cmdLine) + lines2.reduce((s, l) => s + rowsOf(l), 0) + 1 });
    if (/\[ERROR\]|Exception/.test(text)) failures.push(`command ${i} (${clip.items[i].text}) printed an error`);
  }
  if (!out.includes("<<<END>>>")) failures.push("the script did not finish");
  if (!/569/.test(out)) failures.push("569 rows not shown");
  return { transcript: transcript.join("\n") + "\n", layout, failures };
}

/** An app clip: start the app with piped input, type each line when the prompt is shown, collect what a terminal shows. */
async function dryRunApp(clip, conn) {
  const failures = [];
  let transcript = "";
  let layout = [];          // per item: { rows, prompt } (rows = terminal rows from the typed line to the next prompt)
  let child = null, pending = "", exited = null;
  const env = { ...process.env, SECURESHIFT_CONSOLE_WIDTH: String(COLS), ConnectionStrings__SecureShiftDb: conn.text, DOTNET_NOLOGO: "1", DOTNET_CLI_TELEMETRY_OPTOUT: "1" };

  const startApp = () => {
    pending = ""; exited = null;
    child = cp.spawn("dotnet", ["app\\SecureShift.Console.dll"], { cwd: PKG, env });
    child.stdout.on("data", d => { pending += d.toString("utf8").replace(/\uFEFF/g, "").replace(/\r/g, ""); });
    child.stderr.on("data", d => { pending += d.toString("utf8").replace(/\r/g, ""); });
    child.on("exit", code => { exited = code; });
  };
  const waitPrompt = async (timeoutMs = 90e3) => {
    const start = Date.now();
    let lastLen = -1, stableSince = Date.now();
    while (Date.now() - start < timeoutMs) {
      await sleep(40);
      if (pending.length !== lastLen) { lastLen = pending.length; stableSince = Date.now(); continue; }
      if (exited !== null && Date.now() - stableSince > 150) return "exit";
      if (/: $/.test(pending) && Date.now() - stableSince > 150) return "prompt";
    }
    return "timeout";
  };
  const take = () => { const t = pending; pending = ""; return t; };

  let lineStart = "";   // text of the current (unfinished) terminal line, e.g. the prompt
  const segRows = text => {
    // rows from the line where the user typed to the end of `text` (the next prompt line)
    const lines = text.split("\n");
    return lines.reduce((s, l) => s + rowsOf(l), 0);
  };

  const items = clip.items;
  for (let i = 0; i < items.length; i++) {
    const it = items[i];
    if (it.t === "sh") {
      if (it.app) {
        startApp();
        const st = await waitPrompt();
        const out = take();
        transcript += "> " + it.text + "\n" + out;
        const lastNl = out.lastIndexOf("\n");
        lineStart = out.slice(lastNl + 1);
        layout.push({ rows: 1 + segRows(out.slice(0, lastNl)) + 1 });
        if (st !== "prompt") failures.push(`app did not show the login screen (${st})`);
      } else {
        let out = "";
        if (it.file) out = fs.readFileSync(path.join(HERE, "sql", it.file), "utf8").replace(/\r/g, "").replace(/\n+$/, "") + "\n";
        if (it.psql) out = runPsql(conn, [...(/ -e /.test(it.text) ? ["-e"] : []), "-f", path.join(HERE, "sql", it.psql)]);
        transcript += "> " + it.text + "\n" + out;
        layout.push({ rows: rowsOf("> " + it.text) + segRows(out.replace(/\n$/, "")) + 1 });
      }
      continue;
    }
    // a key typed into the app
    if (exited !== null) { failures.push(`item ${i} "${it.text}": the app has already exited`); break; }
    let before = "";
    if (it.teammate) {
      // the teammate's psql output appears on the screen while the app waits at the prompt
      const out = runPsql(conn, ["-e", "-f", path.join(HERE, "sql", it.teammate)]);
      before = out + "(psql done) ";
    }
    const echo = it.pw ? "*".repeat(it.text.length) : it.text;
    const typedLine = lineStart + before + echo;
    transcript += before + echo + "\n";
    child.stdin.write(it.text + "\n");
    const st = await waitPrompt(it.timeout ? it.timeout * 1000 : 90e3);
    const out = take();
    transcript += out;
    const lastNl = out.lastIndexOf("\n");
    const body = lastNl >= 0 ? out.slice(0, lastNl) : "";
    // Enter moved the terminal to a new line: the output starts there; its last line is the next prompt
    const prompt = lastNl >= 0 ? out.slice(lastNl + 1) : out;
    const rows = rowsOf(typedLine) + (lastNl >= 0 ? segRows(body) : 0) + rowsOf(prompt);
    layout.push({ rows, lines: lastNl >= 0 ? body.split("\n").length : 0, err: /\[ERROR\]/.test(out), ok: /\[OK\]|\[INFO\]|\[WARNING\]/.test(out), prompt: prompt.slice(-40) });
    lineStart = prompt;
    if (st === "timeout") failures.push(`item ${i} "${it.text}": no prompt after 90 s`);
    for (const x of [].concat(it.x || [])) if (!out.includes(x)) failures.push(`item ${i} "${echo}": expected "${x}"`);
    if (/\[ERROR\]/.test(out) && !it.err) failures.push(`item ${i} "${echo}": unexpected ${out.match(/\[ERROR\][^\n]*/)[0]}`);
    if (it.err && !/\[ERROR\]/.test(out)) failures.push(`item ${i} "${echo}": an [ERROR] was expected`);
    if (/Unhandled exception|Unexpected problem|\n\s+at [A-Z][\w.]+\(/.test(out)) failures.push(`item ${i} "${echo}": crash / stack trace`);
    if (st === "exit") {
      if (exited !== 0) failures.push(`the app exited with code ${exited}`);
      transcript += "\n";
      lineStart = "";
    }
  }
  if (child && exited === null) { failures.push("the app was still running at the end of the clip"); child.kill(); }
  return { transcript, layout, failures };
}

async function dryRun() {
  const from = Number(arg("--from", 0)), to = Number(arg("--to", 99));
  const outDir = path.join(HERE, "transcripts");
  fs.mkdirSync(outDir, { recursive: true });
  const layoutFile = path.join(HERE, "layout.json");
  const all = fs.existsSync(layoutFile) ? JSON.parse(fs.readFileSync(layoutFile, "utf8")) : {};
  let bad = 0;
  const snapDir = path.join(DEMO, "work", "dry-snap");
  fs.mkdirSync(snapDir, { recursive: true });
  if (from > 0) {
    // start from the database as it was after the previous clip
    const prev = CLIPS.filter(c => c.n < from).pop();
    snapshot("restore", path.join(snapDir, prev.file + ".dump"));
  }
  for (const clip of CLIPS.filter(c => c.n >= from && c.n <= to)) {
    const t0 = Date.now();
    const r = clip.shell ? dryRunShell(clip) : await dryRunApp(clip, readConn());
    if (!r.failures.length) snapshot("save", path.join(snapDir, clip.file + ".dump"));
    fs.writeFileSync(path.join(outDir, clip.file + ".txt"), r.transcript);
    all[clip.file] = r.layout;
    fs.writeFileSync(layoutFile, JSON.stringify(all, null, 1) + "\n");
    const sec = ((Date.now() - t0) / 1000).toFixed(0);
    if (r.failures.length) { bad++; console.log(`[FAIL] ${clip.file} (${sec} s)`); r.failures.forEach(f => console.log("       " + f)); }
    else console.log(`[OK] ${clip.file} (${sec} s)`);
    if (r.failures.length && !process.argv.includes("--keep-going")) break;
  }
  process.exit(bad ? 1 : 0);
}

/* =====================================================================================================
 * 5. Tapes
 * ===================================================================================================== */
const q = s => s.includes('"') ? (s.includes("`") ? `'${s}'` : "`" + s + "`") : `"${s}"`;
const typeTime = s => (s.length + 1) * TYPING;

function pauseFor(it, lay) {
  if (it.p !== undefined) return it.p;
  if (!lay) return 1;
  if (lay.err || lay.ok) return 2;
  if ((lay.lines || 0) >= 4) return 2;
  return 0.7;
}

/** VHS waits for the prompt the dry run saw (the end of it, regex-escaped), e.g. /Choose:\s*$/ or /\(y\/n\):\s*$/. */
function promptRx(lay) {
  const p = (lay && lay.prompt ? lay.prompt : "").trimEnd();
  if (!p.endsWith(":")) return "/:\\s*$/";
  const tail = p.slice(-14).replace(/[\\^$.*+?()[\]{}|/]/g, m => "\\" + m);
  return `/${tail}\\s*$/`;
}

/** Shows a screen that is taller than the terminal: scroll up to its first line, then down again. */
function scrollCmds(rows, pause) {
  const hidden = rows - ROWS;
  if (hidden <= 0) return { cmds: [`Sleep ${pause}s`], time: pause };
  const cmds = [`Sleep 1s`, `ScrollUp ${hidden}`, `Sleep ${Math.max(3, pause)}s`];
  let time = 1 + Math.max(3, pause);
  let left = hidden;
  while (left > 0) { const d = Math.min(ROWS - 5, left); cmds.push(`ScrollDown ${d}`); left -= d; cmds.push(`Sleep ${left > 0 ? 3 : 2}s`); time += left > 0 ? 3 : 2; }
  return { cmds, time };
}

function buildTape(clip, layout) {
  const out = [];
  const lay = layout[clip.file] || [];
  out.push(`# SecureShift full walkthrough - clip ${String(clip.n).padStart(2, "0")}: ${clip.role} - ${clip.task}`);
  out.push(`# ${clip.small}`);
  out.push("# Generated by docs/demo/full/clips.js (do not edit by hand); recorded by scripts/windows/record-full-demo.ps1");
  out.push(`Output raw/${clip.file}.mp4`);
  out.push('Set Shell "powershell"', "Set FontSize 25", "Set Width 1920", "Set Height 1080", "Set Padding 24", "Set TypingSpeed 70ms", "Set Framerate 25", "");
  const prelude = clip.shell
    ? `$ProgressPreference = 'SilentlyContinue'; Set-Location ${clip.cwd}; Clear-Host`
    : `$ProgressPreference = 'SilentlyContinue'; Set-Location ${PKG}; $env:ConnectionStrings__SecureShiftDb = Get-Content ${TOOLS}\\connection-string.txt; $env:PGPASSWORD = Get-Content ${TOOLS}\\postgres-password.txt; $psql = '${TOOLS}\\pgsql\\bin\\psql.exe'; Clear-Host`;
  out.push("Hide", `Type ${q(prelude)}`, "Enter", "Wait@60s />\\s*$/", "Show", "Sleep 1s");

  // timeline (recorded seconds) for the coverage start times + the teammate delays
  let t = 1;
  const marks = [];   // { idx, t }
  const teammateAt = {};   // sql -> t when it must have finished
  const teammateStart = {}; // sql -> line index in `out` + t at start
  const keysSince = {};     // sql -> number of app answers typed since the teammate started
  const flat = [];
  clip.items.forEach((it, i) => { flat.push({ it, lay: lay[flat.length] }); if (it.keys) it.keys.forEach(kk => flat.push({ it: { ...kk, inShell: true }, lay: lay[flat.length] })); });
  // layout has one entry per item (shell keys are counted in the shell command's entry)
  let li = 0;
  const layOf = () => lay[li++];

  for (let i = 0; i < clip.items.length; i++) {
    const it = clip.items[i];
    marks.push({ i, t });
    const l = layOf();
    if (it.t === "sh") {
      if (it.teammate) { out.push("__TEAMMATE__" + it.teammate); keysSince[it.teammate] = 0; }
      else out.push(`Type ${q(it.text)}`);
      out.push("Enter");
      t += typeTime(it.text) + (it.teammate ? 0.2 : 0);
      if (it.teammate) teammateStart[it.teammate] = { line: out.length - 2, t };
      if (it.app) {
        out.push(`Wait@120s /Choose:\\s*$/`, "Sleep 2s");
        t += 2 + 2;   // app start (wait) is not recorded; 2 s pause on the login screen
        continue;
      }
      if (it.keys) {
        out.push(`Wait@${it.timeout || 300}s /Choose:\\s*$/`);
        const s = scrollCmds(l ? l.rows : 0, it.p ?? 2); out.push(...s.cmds); t += s.time;
        for (const kk of it.keys) {
          marks.push({ i, t, key: kk });
          if (kk.text !== "") out.push(`Type ${q(kk.text)}`);
          out.push("Enter", `Wait@${kk.timeout || 120}s />\\s*$/`, `Sleep ${kk.p ?? 2}s`);
          t += typeTime(kk.text) + (kk.p ?? 2);
        }
        continue;
      }
      out.push(`Wait@${it.timeout || 120}s />\\s*$/`);
      const s = scrollCmds(l ? l.rows : 0, it.p ?? 2); out.push(...s.cmds); t += s.time;
      continue;
    }
    // key into the app
    if (it.teammate) { out.push(`Wait@180s /psql done/`); teammateAt[it.teammate] = { t, keys: keysSince[it.teammate] }; out.push("Sleep 2s"); t += 2; }
    if (it.text !== "") out.push(`Type ${q(it.text)}`);   // an empty answer = only Enter (keep the value / the default)
    out.push("Enter", "Sleep 300ms");
    t += typeTime(it.text) + 0.3;
    for (const sql of Object.keys(keysSince)) keysSince[sql]++;
    const next = clip.items[i + 1];
    const appEnds = !next || next.t === "sh";   // the last key of an app session exits the app: back to the PowerShell prompt
    out.push(appEnds ? `Wait@60s />\\s*$/` : `Wait@${it.timeout || 120}s ${promptRx(l)}`);
    const s = scrollCmds(l ? l.rows : 0, pauseFor(it, l));
    out.push(...s.cmds); t += s.time;
  }
  out.push("Sleep 1s");
  t += 1;
  // The teammate delays: from the Start-Process command to the moment the change must be visible. Recorded time
  // (typing + pauses) plus what VHS does not record (app start ~4 s, EF warm-up / login ~3 s, ~0.5 s per answer)
  // plus a margin. Too long costs nothing: the tape waits for "(psql done)" and VHS does not record the wait.
  for (const [sql, st] of Object.entries(teammateStart)) {
    const fire = teammateAt[sql];
    const delay = Math.ceil((fire.t - st.t) + 4 + 3 + 0.5 * fire.keys + 8);
    const cmd = `Teammate ${LIVE}\\${sql} ${delay}`;
    out[st.line] = `Type ${q(cmd)}`;
  }
  return { text: out.join("\n") + "\n", marks, total: t };
}

function writeTapes() {
  const layoutFile = path.join(HERE, "layout.json");
  if (!fs.existsSync(layoutFile)) { console.error("layout.json missing: run the dry run first"); process.exit(1); }
  const layout = JSON.parse(fs.readFileSync(layoutFile, "utf8"));
  const dir = path.join(HERE, "tapes");
  fs.mkdirSync(dir, { recursive: true });
  for (const f of fs.readdirSync(dir)) if (f.endsWith(".tape")) fs.unlinkSync(path.join(dir, f));
  const plan = {};
  for (const clip of CLIPS) {
    const tape = buildTape(clip, layout);
    fs.writeFileSync(path.join(dir, clip.file + ".tape"), tape.text);   // UTF-8 without BOM
    plan[clip.file] = { estimated: Number(tape.total.toFixed(2)), marks: tape.marks.map(m => Number(m.t.toFixed(2))) };
  }
  fs.writeFileSync(path.join(HERE, "timeline.json"), JSON.stringify(plan, null, 1) + "\n");
  console.log(`wrote ${CLIPS.length} tapes to ${path.relative(process.cwd(), dir)}`);
}

/* =====================================================================================================
 * 6. Docs: COVERAGE.md, CHAPTERS.md, chapters-youtube.txt, SecureShift-FULL-walkthrough.srt
 * ===================================================================================================== */
const pad2 = n => String(n).padStart(2, "0");
function clock(sec, long) {
  sec = Math.max(0, Math.floor(sec));
  const h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = sec % 60;
  return long || h > 0 ? `${h}:${pad2(m)}:${pad2(s)}` : `${pad2(m)}:${pad2(s)}`;
}
function srtTime(sec) {
  const ms = Math.round(sec * 1000);
  const h = Math.floor(ms / 3600000), m = Math.floor((ms % 3600000) / 60000), s = Math.floor((ms % 60000) / 1000), r = ms % 1000;
  return `${pad2(h)}:${pad2(m)}:${pad2(s)},${String(r).padStart(3, "0")}`;
}
const title = c => `Step ${pad2(c.n)} – ${c.role} – ${c.task}`;
const VIDEOS = [
  ["01-setup-and-login", "Setup from zero (offline package) and login"],
  ["02-super-admin", "Super Admin"], ["03-cloud-admin", "Cloud Admin (Cloud Governance)"], ["04-admin", "Admin (Application Modernization)"],
  ["05-architect", "Architect"], ["06-executive", "Executive (+ profile, change password, exit)"], ["07-live-db-change", "Live database change (psql) while the app runs"],
];

function writeDocs(timingsFile) {
  const tm = timingsFile && fs.existsSync(timingsFile) ? JSON.parse(fs.readFileSync(timingsFile, "utf8")) : null;
  const timeline = fs.existsSync(path.join(HERE, "timeline.json")) ? JSON.parse(fs.readFileSync(path.join(HERE, "timeline.json"), "utf8")) : {};
  const long = tm ? tm.total >= 3600 : false;
  const startOf = {};     // clip file -> start of its CONTENT in the full video (after the title card)
  if (tm) for (const c of tm.clips) startOf[c.file] = c.start + (c.card || tm.card);
  const { rows, missing } = checkCoverage(false);

  // offset of a coverage row inside its clip: estimated timeline scaled to the real clip length
  const rowTime = r => {
    if (!tm) return "";
    const c = tm.clips.find(x => x.file === r.clip.file);
    const tl = timeline[r.clip.file];
    if (!c || !tl) return "";
    // r.idx counts the flattened items (shell keys included); timeline.marks has one mark per item + one per shell key
    const est = tl.marks[Math.min(r.idx, tl.marks.length - 1)] || 0;
    const scale = tl.estimated > 0 ? c.duration / tl.estimated : 1;
    return clock(startOf[r.clip.file] + est * scale, long);
  };

  const md = [];
  md.push("# SecureShift full walkthrough - coverage", "");
  md.push("Every menu option of every role (main menus, sub-menus, filters, paging, search, back / exit) and every important error case,");
  md.push("in the order they appear in **SecureShift-FULL-walkthrough.mp4**. *What is typed* is exactly what the clip types (`⏎` = Enter only,");
  md.push("`*****` = a password, shown masked by the app). Generated by `node docs/demo/full/clips.js docs` from [clips.js](clips.js).", "");
  md.push(`**Coverage: ${OPTIONS.length - missing.length} / ${OPTIONS.length} options and error cases (${Math.round(100 * (OPTIONS.length - missing.length) / OPTIONS.length)} %)**` + (missing.length ? ` - not covered: ${missing.join(", ")}` : ""), "");
  md.push("| Step no | Role | Menu path | What is typed | Expected result | Clip file | Start time in the full video |");
  md.push("|---:|---|---|---|---|---|---|");
  const esc = s => String(s).replace(/\|/g, "\\|").replace(/\n/g, " ");
  rows.forEach((r, i) => {
    const o = r.ids.map(id => OPT.get(id)).filter(Boolean);
    const typed = r.typed.join(" · ");
    md.push(`| ${i + 1} | ${esc(o[0]?.role ?? "?")} | ${esc(o.map(x => x.menu).join(" + "))} | \`${esc(typed.length > 160 ? typed.slice(0, 157) + "..." : typed)}\` | ${esc(o.map(x => x.expected).join(" · "))} | [${r.clip.file}](tapes/${r.clip.file}.tape) | ${rowTime(r)} |`);
  });
  md.push("", "## Checklist per role", "");
  for (const role of [...new Set(OPTIONS.map(o => o[1]))]) {
    const list = OPTIONS.filter(o => o[1] === role);
    md.push(`**${role}** (${list.length}): ` + list.map(o => {
      const r = rows.findIndex(x => x.ids.includes(o[0]));
      return r >= 0 ? `${o[2]} (step ${r + 1})` : `${o[2]} (**missing**)`;
    }).join(" · "), "");
  }
  fs.writeFileSync(path.join(HERE, "COVERAGE.md"), md.join("\n") + "\n");

  if (!tm) { console.log("wrote COVERAGE.md (no timings yet)"); return; }
  const ch = ["# SecureShift full walkthrough - chapters", "", `SecureShift-FULL-walkthrough.mp4 · ${clock(tm.total, true)} · ${CLIPS.length} steps · each step starts with a ${tm.card} s title card.`, "", "| Start | Step | Title |", "|---|---|---|"];
  const yt = [];
  for (const c of CLIPS) {
    const s = tm.clips.find(x => x.file === c.file).start;
    ch.push(`| ${clock(s, long)} | ${pad2(c.n)} | ${c.role} – ${c.task} |`);
    yt.push(`${clock(s, long)} ${title(c)}`);
  }
  ch.push("", "## Per-role videos", "", "| Video | Steps | Length |", "|---|---|---|");
  for (const [v, name] of VIDEOS) {
    const cs = CLIPS.filter(c => c.video === v);
    ch.push(`| SecureShift-${v}.mp4 | ${cs.map(c => pad2(c.n)).join(", ")} (${name}) | ${clock(tm.videos[v] || 0, true)} |`);
  }
  ch.push("", "## YouTube chapter list", "", "```", ...yt, "```");
  fs.writeFileSync(path.join(HERE, "CHAPTERS.md"), ch.join("\n") + "\n");
  fs.writeFileSync(path.join(HERE, "chapters-youtube.txt"), yt.join("\n") + "\n");
  const srt = [];
  CLIPS.forEach((c, i) => {
    const s = tm.clips.find(x => x.file === c.file).start;
    srt.push(String(i + 1), `${srtTime(s)} --> ${srtTime(s + tm.card + 3)}`, title(c), c.small, "");
  });
  fs.writeFileSync(path.join(HERE, "SecureShift-FULL-walkthrough.srt"), srt.join("\r\n"));
  console.log("wrote COVERAGE.md, CHAPTERS.md, chapters-youtube.txt, SecureShift-FULL-walkthrough.srt");
}

/* =====================================================================================================
 * 7. main
 * ===================================================================================================== */
const cmd = process.argv[2];
if (cmd === "list") {
  for (const c of CLIPS) console.log(`${pad2(c.n)}  ${c.file.padEnd(42)} ${c.video.padEnd(20)} ${c.role} - ${c.task}`);
} else if (cmd === "coverage") {
  const r = checkCoverage(true);
  process.exit(r.missing.length || r.unknown.length ? 1 : 0);
} else if (cmd === "dryrun") {
  dryRun().catch(e => { console.error(e); process.exit(1); });
} else if (cmd === "tapes") {
  writeTapes();
} else if (cmd === "docs") {
  writeDocs(process.argv[3]);
} else if (cmd === "json") {
  // for the PowerShell recorder: the clips with their titles and videos
  console.log(JSON.stringify(CLIPS.map(c => ({ n: c.n, file: c.file, role: c.role, task: c.task, small: c.small, video: c.video, title: title(c) })), null, 1));
} else {
  console.log("usage: node clips.js list | coverage | dryrun [--from N] [--to N] [--keep-going] | tapes | docs <timings.json> | json");
  process.exit(1);
}

module.exports = { CLIPS, OPTIONS };
