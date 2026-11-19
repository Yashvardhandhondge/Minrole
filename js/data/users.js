/* =========================================================
   SecureShift - users.js
   The 5 login accounts. Each has its own email, password and role.
   After login, each user goes to the dashboard of their role (home).
   ========================================================= */

window.SS = window.SS || {};

SS.ACCOUNTS = [
  /* ----- Cloud Governance ----- */
  { id: "U-M01", product: "governance", role: "cloudadmin", roleLabel: "Cloud Admin",
    name: "Aarav Mehta", email: "aarav.mehta@secureshift.io", password: "Cloud@2026", home: "governance/dashboard.html" },

  /* ----- EMDT (Application Modernization) ----- */
  { id: "U-E01", product: "emdt", role: "superadmin", roleLabel: "Super Admin",
    name: "Arya Mujumdar", email: "arya.mujumdar@secureshift.io", password: "Super@2026", home: "modernization/superadmin-dashboard.html" },
  { id: "U-E02", product: "emdt", role: "admin", roleLabel: "Admin",
    name: "Sachin Kumar", email: "sachin.kumar@secureshift.io", password: "Admin@2026", home: "modernization/admin-dashboard.html" },
  { id: "U-E03", product: "emdt", role: "architect", roleLabel: "Architect",
    name: "Amanjeet Sharma", email: "amanjeet.sharma@secureshift.io", password: "Architect@2026", home: "modernization/architect-dashboard.html" },
  { id: "U-E04", product: "emdt", role: "executive", roleLabel: "Executive",
    name: "Shivanand Kumatekar", email: "shivanand.kumatekar@secureshift.io", password: "Exec@2026", home: "modernization/executive-dashboard.html" }
];

/* Password of an account. A password changed on the Profile page is saved and used next time. */
SS.passwordOf = function (acc) { return SS.store.get("pw." + acc.id, acc.password); };
SS.setPassword = function (id, pw) { SS.store.set("pw." + id, pw); };

SS.PRODUCTS = {
  governance: { name: "Cloud Governance", short: "Cloud Governance", tag: "Least-Privilege Access Suite",
             title: "Single Source of Truth Access Governance", subtitle: "Azure RBAC Least-Privilege Portal 2026" },
  emdt: { name: "Application Modernization - EMDT", short: "EMDT", tag: "Enterprise Modernization Digital Twin",
          title: "Enterprise Modernization Digital Twin", subtitle: "Plan, simulate and approve modernization 2026" }
};
