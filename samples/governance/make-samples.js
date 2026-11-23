// Generates the sample input files of the Cloud Governance analysis (run once: node samples/governance/make-samples.js).
// The files look like Azure CLI exports:
//   role-assignments.json   ~ az role assignment list --all
//   activity-log-90d.json   ~ az monitor activity-log list (only the fields SecureShift reads)
//   roles-catalog.json      ~ az role definition list (actions listed one by one)
// The data follows the prototype (governance-data.js): the same identities, roles, scopes and the
// used permissions of svc-webapp-deploy. Deterministic (fixed random seed), so the files never change.
"use strict";
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const OUT = __dirname;
const SUB = "6f1c2a34-9d8e-4b7a-a1c2-3e4f5a6b7c8d";
const PERIOD_FROM = "2026-08-12", PERIOD_TO = "2026-11-09";   // same period as AN-2026-1110 in the seed

let seed = 7;
function rnd() { seed = (seed * 9301 + 49297) % 233280; return seed / 233280; }
function guid(text) {
  const h = crypto.createHash("md5").update(text).digest("hex");
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20, 32)}`;
}

/* ---------- the actions (resource type -> operations) ---------- */
const TYPES = {
  "Microsoft.Web/sites": ["read", "write", "delete", "config/read", "config/write", "slots/read", "slots/write", "restart/action", "backup/action"],
  "Microsoft.Web/serverfarms": ["read", "write", "delete"],
  "Microsoft.Resources/deployments": ["read", "write", "delete"],
  "Microsoft.Resources/subscriptions/resourceGroups": ["read", "write", "delete"],
  "Microsoft.Insights/components": ["read", "write"],
  "Microsoft.Insights/metrics": ["read"],
  "Microsoft.Insights/diagnosticSettings": ["read", "write"],
  "Microsoft.Compute/virtualMachines": ["read", "write", "delete", "start/action", "deallocate/action", "restart/action"],
  "Microsoft.Compute/disks": ["read", "write", "delete"],
  "Microsoft.Network/virtualNetworks": ["read", "write", "delete"],
  "Microsoft.Network/networkSecurityGroups": ["read", "write", "delete"],
  "Microsoft.Network/networkSecurityGroups/securityRules": ["read", "write", "delete"],
  "Microsoft.Network/publicIPAddresses": ["read", "write", "delete"],
  "Microsoft.Network/loadBalancers": ["read", "write"],
  "Microsoft.Storage/storageAccounts": ["read", "write", "delete", "listKeys/action"],
  "Microsoft.Storage/storageAccounts/blobServices/containers": ["read", "write", "delete"],
  "Microsoft.KeyVault/vaults": ["read", "write", "delete"],
  "Microsoft.KeyVault/vaults/secrets": ["read", "write"],
  "Microsoft.Sql/servers": ["read", "write", "delete"],
  "Microsoft.Sql/servers/databases": ["read", "write", "delete"],
  "Microsoft.Authorization/roleAssignments": ["read", "write", "delete"],
  "Microsoft.Authorization/roleDefinitions": ["read", "write"],
  "Microsoft.Authorization/policyAssignments": ["read", "write"],
  "Microsoft.ContainerService/managedClusters": ["read", "write", "delete", "listClusterUserCredential/action"],
  "Microsoft.RecoveryServices/vaults": ["read", "write"],
  "Microsoft.RecoveryServices/vaults/backupJobs": ["read"],
  "Microsoft.OperationalInsights/workspaces": ["read", "write"],
};
const ALL = Object.keys(TYPES).flatMap(t => TYPES[t].map(op => `${t}/${op}`));
const isRead = a => a.endsWith("/read");
const under = (...prefixes) => ALL.filter(a => prefixes.some(p => a.startsWith(p)));
const readsOf = (...prefixes) => under(...prefixes).filter(isRead);
const uniq = list => [...new Set(list)];

const ROLES = {
  "Owner": ALL,
  "Contributor": ALL.filter(a => !(a.startsWith("Microsoft.Authorization/") && !isRead(a))),
  "Reader": ALL.filter(isRead),
  "Virtual Machine Contributor": uniq([...under("Microsoft.Compute/"), ...readsOf("Microsoft.Network/", "Microsoft.Storage/storageAccounts/", "Microsoft.Resources/", "Microsoft.Insights/metrics/")]),
  "Backup Contributor": uniq([...under("Microsoft.RecoveryServices/"), "Microsoft.Compute/virtualMachines/read", ...readsOf("Microsoft.Resources/")]),
  "Network Contributor": uniq([...under("Microsoft.Network/"), ...under("Microsoft.Resources/deployments/"), "Microsoft.Resources/subscriptions/resourceGroups/read"]),
  "AKS Contributor": uniq([...under("Microsoft.ContainerService/"), ...readsOf("Microsoft.Resources/", "Microsoft.Network/")]),
  "Storage Blob Data Contributor": uniq([...under("Microsoft.Storage/storageAccounts/blobServices/"), "Microsoft.Storage/storageAccounts/read"]),
  "SQL DB Contributor": uniq([...under("Microsoft.Sql/servers/databases/"), "Microsoft.Sql/servers/read", "Microsoft.Insights/metrics/read", ...readsOf("Microsoft.Resources/")]),
  "Key Vault Officer": uniq([...under("Microsoft.KeyVault/"), ...readsOf("Microsoft.Resources/")]),
  "Website Contributor": uniq([...under("Microsoft.Web/sites/"), "Microsoft.Web/serverfarms/read", "Microsoft.Insights/components/read", ...readsOf("Microsoft.Resources/")]),
  "Monitoring Reader": uniq([...readsOf("Microsoft.Insights/", "Microsoft.OperationalInsights/"), ...readsOf("Microsoft.Resources/")]),
};
const roleId = name => `/subscriptions/${SUB}/providers/Microsoft.Authorization/roleDefinitions/${guid("role:" + name)}`;

/* ---------- identities (governance-data.js) ---------- */
// [userName, displayName, type, role, resourceGroup or null (= Subscription), used actions or a count]
const P = [
  ["svc-webapp-deploy", "Web App Deploy Pipeline", "ServicePrincipal", "Contributor", null, "webapp"],
  ["rahul.verma", "Rahul Verma", "User", "Owner", null, 25],
  ["priya.sharma", "Priya Sharma", "User", "Contributor", "rg-sales", 30],
  ["svc-data-pipeline", "Data Pipeline Service", "ServicePrincipal", "Virtual Machine Contributor", "rg-data", 14],
  ["mi-orders-api", "Orders API (Managed)", "ManagedIdentity", "Reader", "rg-orders", 30],
  ["svc-backup", "Backup Service", "ServicePrincipal", "Backup Contributor", "rg-backup", 6],
  ["neha.kulkarni", "Neha Kulkarni", "User", "Network Contributor", "rg-network", 10],
  ["mi-reports-func", "Reports Function (Managed)", "ManagedIdentity", "Contributor", "rg-reports", 8],
  ["amit.joshi", "Amit Joshi", "User", "Contributor", "rg-hr", 20],
  ["svc-aks-ops", "AKS Operations Service", "ServicePrincipal", "AKS Contributor", "rg-aks", 5],
  ["kavya.iyer", "Kavya Iyer", "User", "Reader", null, 36],
  ["mi-billing-job", "Billing Job (Managed)", "ManagedIdentity", "Storage Blob Data Contributor", "rg-billing", 3],
  ["svc-github-ci", "GitHub CI Pipeline", "ServicePrincipal", "Owner", "rg-webapp-prod", 10],
  ["rohan.desai", "Rohan Desai", "User", "SQL DB Contributor", "rg-data", 6],
  ["mi-notify-func", "Notify Function (Managed)", "ManagedIdentity", "Contributor", "rg-notify", 7],
  ["sneha.patil", "Sneha Patil", "User", "Key Vault Officer", "rg-security", 4],
  ["deepak.shinde", "Deepak Shinde", "User", "Website Contributor", "rg-catalog", 9],
  ["svc-etl-020", "Etl Service", "ServicePrincipal", "Contributor", "rg-etl", 12],
  ["mi-search-021", "Search Function (Managed)", "ManagedIdentity", "Monitoring Reader", "rg-search", 5],
  ["pooja.patil", "Pooja Patil", "User", "Reader", "rg-crm", 15],
];

// svc-webapp-deploy: the 12 used permissions of the prototype (SS.GOV.detail) + 2 rare ones (roles.js)
const WEBAPP = [
  ["Microsoft.Web/sites/write", "2026-08-14", "2026-11-09", 46], ["Microsoft.Web/sites/config/write", "2026-08-14", "2026-11-09", 31],
  ["Microsoft.Web/sites/slots/write", "2026-08-20", "2026-11-08", 18], ["Microsoft.Web/sites/restart/action", "2026-09-01", "2026-11-07", 9],
  ["Microsoft.Resources/deployments/write", "2026-08-14", "2026-11-09", 52], ["Microsoft.Web/sites/read", "2026-08-14", "2026-11-09", 212],
  ["Microsoft.Web/sites/config/read", "2026-08-14", "2026-11-09", 140], ["Microsoft.Web/serverfarms/read", "2026-08-15", "2026-11-09", 64],
  ["Microsoft.Insights/components/read", "2026-08-18", "2026-11-09", 27], ["Microsoft.Resources/deployments/read", "2026-08-14", "2026-11-09", 52],
  ["Microsoft.Web/sites/slots/read", "2026-08-20", "2026-11-08", 18], ["Microsoft.Resources/subscriptions/resourceGroups/read", "2026-08-14", "2026-11-09", 88],
  ["Microsoft.Web/sites/delete", "2026-08-16", "2026-08-16", 1], ["Microsoft.Web/sites/backup/action", "2026-08-31", "2026-08-31", 1],
];

const day = 86400000;
const from = Date.parse(PERIOD_FROM + "T00:00:00Z"), to = Date.parse(PERIOD_TO + "T00:00:00Z");
function stamp(ms) {
  const h = Math.floor(rnd() * 10) + 8, m = Math.floor(rnd() * 60), s = Math.floor(rnd() * 60);
  return new Date(ms + ((h * 60 + m) * 60 + s) * 1000).toISOString().replace(".000Z", "Z");
}
function spread(firstMs, lastMs, n) {
  const out = [];
  for (let i = 0; i < n; i++) out.push(n === 1 ? firstMs : firstMs + Math.round(((lastMs - firstMs) / day) * (i / (n - 1))) * day);
  return out;
}

const assignments = [], events = [];
for (const [user, display, type, role, rg, usage] of P) {
  const upn = type === "User" ? `${user}@northwindtraders.com` : user;
  const scope = rg ? `/subscriptions/${SUB}/resourceGroups/${rg}` : `/subscriptions/${SUB}`;
  const principalId = guid("principal:" + user);
  assignments.push({
    id: `${scope}/providers/Microsoft.Authorization/roleAssignments/${guid("ra:" + user)}`,
    principalId, principalName: upn, principalType: type, displayName: display,
    roleDefinitionId: roleId(role), roleDefinitionName: role, scope,
  });

  const workRg = rg || (user === "svc-webapp-deploy" ? "rg-webapp-prod" : "rg-finance");
  let used;
  if (usage === "webapp") used = WEBAPP;
  else {
    const granted = ROLES[role];
    // used actions: mostly reads and normal writes, never the dangerous ones (they stay unused -> findings)
    const safe = granted.filter(a => !/\/delete$|Microsoft\.Authorization\/.*\/write|Microsoft\.KeyVault\/vaults\/write|networkSecurityGroups\/write/.test(a));
    const pick = [];
    let i = Math.floor(rnd() * safe.length);
    while (pick.length < Math.min(usage, safe.length)) { const a = safe[i % safe.length]; if (!pick.includes(a)) pick.push(a); i += 1 + Math.floor(rnd() * 3); }
    if (user === "rahul.verma") pick.push("Microsoft.Authorization/policyAssignments/write");   // rare (roles.js)
    used = pick.map(a => {
      const rare = a === "Microsoft.Authorization/policyAssignments/write";
      const n = rare ? 1 : 2 + Math.floor(rnd() * 22);
      const f = from + Math.floor(rnd() * 30) * day, l = to - Math.floor(rnd() * 20) * day;
      const iso = ms => new Date(ms).toISOString().slice(0, 10);
      return rare ? [a, "2026-09-10", "2026-09-10", 1] : [a, iso(f), iso(Math.max(f, l)), n];
    });
  }
  for (const [action, first, last, n] of used) {
    for (const ms of spread(Date.parse(first + "T00:00:00Z"), Date.parse(last + "T00:00:00Z"), n)) {
      events.push({ caller: upn, operationName: { value: action }, eventTimestamp: stamp(ms), resourceGroupName: workRg, status: { value: "Succeeded" } });
    }
  }
}
// noise that the analysis must ignore: a failed call, an event before the period, an unknown caller
events.push({ caller: "rahul.verma@northwindtraders.com", operationName: { value: "Microsoft.Compute/virtualMachines/delete" }, eventTimestamp: "2026-10-01T10:15:00Z", resourceGroupName: "rg-finance", status: { value: "Failed" } });
events.push({ caller: "priya.sharma@northwindtraders.com", operationName: { value: "Microsoft.Sql/servers/databases/delete" }, eventTimestamp: "2026-07-01T09:00:00Z", resourceGroupName: "rg-sales", status: { value: "Succeeded" } });
events.push({ caller: "old-contractor@northwindtraders.com", operationName: { value: "Microsoft.Web/sites/read" }, eventTimestamp: "2026-09-15T12:00:00Z", resourceGroupName: "rg-sales", status: { value: "Succeeded" } });
events.sort((a, b) => a.eventTimestamp.localeCompare(b.eventTimestamp));

const catalog = Object.keys(ROLES).map(name => ({
  id: roleId(name), name: guid("role:" + name), roleName: name, roleType: "BuiltInRole",
  description: `${name} (sample catalog, actions listed one by one)`,
  permissions: [{ actions: ROLES[name], notActions: [] }],
}));

function write(file, data, oneLinePerItem) {
  const text = oneLinePerItem ? "[\n" + data.map(x => "  " + JSON.stringify(x)).join(",\n") + "\n]\n" : JSON.stringify(data, null, 2) + "\n";
  fs.mkdirSync(path.dirname(path.join(OUT, file)), { recursive: true });
  fs.writeFileSync(path.join(OUT, file), text);
  console.log(`${file}: ${Array.isArray(data) ? data.length + " records, " : ""}${Buffer.byteLength(text)} bytes`);
}
write("role-assignments.json", assignments);
write("activity-log-90d.json", events, true);
write("roles-catalog.json", catalog);

// files for the error demo
write("bad/role-assignments-missing-fields.json", [
  { principalId: guid("principal:rahul.verma"), principalName: "rahul.verma@northwindtraders.com", principalType: "User", displayName: "Rahul Verma", roleDefinitionId: roleId("Owner"), roleDefinitionName: "Owner", scope: `/subscriptions/${SUB}` },
  { principalName: "priya.sharma@northwindtraders.com", principalType: "User", displayName: "Priya Sharma", roleDefinitionId: roleId("Contributor"), roleDefinitionName: "Contributor" },
]);
// valid JSON but the wrong shape: an object instead of a list of role assignments
fs.writeFileSync(path.join(OUT, "bad/role-assignments-wrong-shape.json"),
  JSON.stringify({ subscription: SUB, note: "an object, not a list of role assignments", count: 20 }, null, 2) + "\n");
console.log("bad/role-assignments-wrong-shape.json: a JSON object instead of a list");
fs.writeFileSync(path.join(OUT, "bad/activity-log-broken.json"),
  '[\n  {"caller": "rahul.verma@northwindtraders.com", "operationName": {"value": "Microsoft.Web/sites/read"}, "eventTimestamp": "2026-09-01T10:00:00Z"\n  {"caller": "priya.sharma@northwindtraders.com"\n');
console.log("bad/activity-log-broken.json: broken on purpose (missing comma and closing brackets)");
