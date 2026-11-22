# Governance sample input files

The Cloud Admin **Run Analysis (Analysis Wizard)** reads 3 JSON files. Type their paths when the wizard asks
(or press Enter to use the sample file shown in brackets). These samples are made by `make-samples.js`
(`node samples/governance/make-samples.js`) and are copied next to the app when it is built.

| File | Looks like | What SecureShift reads |
|---|---|---|
| `role-assignments.json` | `az role assignment list --all` | `principalId`, `principalName`, `principalType` (User / ServicePrincipal / ManagedIdentity), `displayName`, `roleDefinitionId`, `roleDefinitionName`, `scope` |
| `activity-log-90d.json` | `az monitor activity-log list` | `caller`, `operationName.value`, `eventTimestamp`, `resourceGroupName`, `status.value` |
| `roles-catalog.json` | `az role definition list` | `id`, `roleName`, `permissions[].actions`, `permissions[].notActions` |

Each file may be a JSON array, or an object with a `"value"` array (the Azure REST format).

**Period:** the sample activity log covers **08/12/2026 - 11/09/2026**. In the wizard choose
`4. Custom range` and type `08/12/2026` and `11/09/2026` (the "Last N days" choices only work while
those dates are inside the last N days).

## The 4 checks (step 2 of the wizard, same texts as the prototype)

1. **Schema validation** - every file is valid JSON and a list of objects.
2. **Missing fields check** - principalId, roleDefinitionId and scope present in every record (and every role
   of an assignment is in the roles catalog).
3. **Duplicate data check** - no role assignment twice; one role assignment per identity.
4. **Activity coverage** - the activity log has successful events of these identities inside the period.
   Failed calls, events outside the period and unknown callers are ignored (the wizard prints how many).

## Files for the error demo (`bad/`)

- `bad/role-assignments-missing-fields.json` - record 2 has no `principalId` and no `scope`
  → `Missing fields check failed: role-assignments-missing-fields.json record 2 has no principalId, scope`
- `bad/activity-log-broken.json` - not valid JSON (a comma and the closing brackets are missing)
  → `Schema validation failed: activity-log-broken.json is not valid JSON (line 3, position 3) ...`

## Scoring (the prototype shows the 3 signals; SecureShift calculates them)

```
unused ratio  = Math.round((1 - used / granted) * 100)            (SS.GOV.getDetail)
high-risk     = min(100, unused granted actions matching an enabled high-risk pattern x 10)
scope breadth = Management Group / Subscription 100, Resource Group 40, Resource 10
score         = Math.round((wUnused x ratio + wHighRisk x high-risk + wScope x scope) / 100)   (Rule Settings weights)
risk level    = >= 70 HIGH, >= 40 MEDIUM, else LOW                 (SS.GOV.riskOf)
```

With the sample files, `svc-webapp-deploy` (Contributor on the whole subscription, 14 of 76 actions used,
20 unused high-risk actions) scores **91**, the same as in the prototype.
