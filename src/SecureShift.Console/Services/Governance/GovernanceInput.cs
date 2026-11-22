using System.Text.Json;

namespace SecureShift.ConsoleApp.Services.Governance;

// The 3 input files of the Analysis Wizard (governance/js/analysis.js), read with System.Text.Json.
// These records describe the FILE format (they are not copies of database entities).

/// <summary>One record of role-assignments.json (az role assignment list).</summary>
public sealed record RoleAssignmentRow(int Number, string PrincipalId, string PrincipalName, string IdentityType, string DisplayName,
    string RoleDefinitionId, string? RoleDefinitionName, string ScopeText, string Scope, string? ScopeName)
{
    /// <summary>The user name stored in dev."Identities": rahul.verma@northwindtraders.com -> rahul.verma.</summary>
    public string UserName => PrincipalName.Contains('@') ? PrincipalName[..PrincipalName.IndexOf('@')] : PrincipalName;
}

/// <summary>One event of activity-log.json (az monitor activity-log list).</summary>
public sealed record ActivityEventRow(string Caller, string Action, DateTimeOffset Timestamp, string? ResourceGroup, string Status);

/// <summary>One role of roles-catalog.json (az role definition list).</summary>
public sealed record CatalogRole(string Id, string RoleName, IReadOnlyList<string> Actions);

/// <summary>One line of the validation step ("Schema validation" ... "Activity coverage").</summary>
public sealed record ValidationStep(string Name, bool Ok, string Message);

/// <summary>The 3 files after validation, ready for the analysis.</summary>
public sealed record GovernanceInput(
    IReadOnlyList<RoleAssignmentRow> Assignments,
    IReadOnlyList<ActivityEventRow> Events,
    IReadOnlyList<CatalogRole> Roles,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<ValidationStep> Steps,
    string Summary)
{
    public int PeriodDays => PeriodTo.DayNumber - PeriodFrom.DayNumber + 1;
}

/// <summary>A friendly problem with an input file (missing file, bad JSON, wrong shape). Shown as [ERROR]; nothing is saved.</summary>
public sealed class GovernanceInputException(string message, IReadOnlyList<ValidationStep> steps) : Exception(message)
{
    public IReadOnlyList<ValidationStep> Steps { get; } = steps;
}

/// <summary>Reads and validates the 3 JSON files (step 1 "Upload" + step 2 "Validate" of the wizard).</summary>
public static class GovernanceInputReader
{
    public const string AssignmentsTitle = "Role Assignments (JSON)";
    public const string ActivityTitle = "Activity Log (JSON)";
    public const string CatalogTitle = "Roles Catalog (JSON)";

    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["User"] = "User",
        ["ServicePrincipal"] = "Service Principal",
        ["Service Principal"] = "Service Principal",
        ["ManagedIdentity"] = "Managed Identity",
        ["Managed Identity"] = "Managed Identity",
    };

    /// <summary>analysis.js periodInfo(): "" when the period is valid, otherwise the prototype message.</summary>
    public static string CheckPeriod(DateOnly from, DateOnly to, DateOnly today)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        if (to > today) return "To date cannot be in the future";
        if (days < 1) return "From date must be before To date";
        if (days < 7) return "Period must be at least 7 days";
        if (days > 180) return "Period cannot be longer than 180 days";
        return "";
    }

    /// <summary>
    /// A path typed by the user → full path of an existing .json file, or a friendly error.
    /// Quotes are removed (Windows adds them when a file is dragged into the terminal).
    /// </summary>
    public static string ResolvePath(string typed, string title)
    {
        var p = typed.Trim().Trim('"', '\'').Trim();
        if (p.Length == 0) throw new UserFacingException($"{title}: enter the path of the JSON file.");
        if (!p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new UserFacingException($"{title}: please choose a .json file ({p}).");
        var full = Path.GetFullPath(p);
        if (!File.Exists(full))
            throw new UserFacingException($"{title}: file not found: {full}. Check the path (tip: drag the file into this window to paste its full path).");
        return full;
    }

    public static async Task<GovernanceInput> LoadAsync(string assignmentsPath, string activityPath, string catalogPath,
        DateOnly from, DateOnly to, string companyName)
    {
        var steps = new List<ValidationStep>();
        void Fail(string step, string message)
        {
            steps.Add(new ValidationStep(step, false, message));
            throw new GovernanceInputException($"{step} failed: {message}", steps);
        }

        // ---- 1. Schema validation: valid JSON, an array of objects (or {"value": [...]}) ----
        var a = await ReadArrayAsync(assignmentsPath, "role assignments", m => Fail("Schema validation", m));
        var e = await ReadArrayAsync(activityPath, "activity log events", m => Fail("Schema validation", m));
        var c = await ReadArrayAsync(catalogPath, "role definitions", m => Fail("Schema validation", m));
        steps.Add(new ValidationStep("Schema validation", true, "All 3 files match the expected JSON structure"));

        // ---- 2. Missing fields check ----
        var problems = new List<string>();
        var assignments = new List<RoleAssignmentRow>();
        for (var i = 0; i < a.Count; i++)
        {
            var r = a[i];
            var missing = new[] { "principalId", "principalName", "principalType", "roleDefinitionId", "scope" }.Where(f => Str(r, f) is null).ToList();
            if (missing.Count > 0)
            {
                problems.Add($"{Path.GetFileName(assignmentsPath)} record {i + 1} has no {string.Join(", ", missing)}");
                continue;
            }
            if (!TypeNames.TryGetValue(Str(r, "principalType")!, out var type))
            {
                problems.Add($"{Path.GetFileName(assignmentsPath)} record {i + 1}: principalType '{Str(r, "principalType")}' must be User, ServicePrincipal or ManagedIdentity");
                continue;
            }
            var scopeText = Str(r, "scope")!;
            var (scope, scopeName) = ParseScope(scopeText);
            if (scope is null)
            {
                problems.Add($"{Path.GetFileName(assignmentsPath)} record {i + 1}: scope '{scopeText}' is not an Azure scope (/subscriptions/... )");
                continue;
            }
            var principalName = Str(r, "principalName")!;
            assignments.Add(new RoleAssignmentRow(i + 1, Str(r, "principalId")!, principalName, type, Str(r, "displayName") ?? principalName,
                Str(r, "roleDefinitionId")!, Str(r, "roleDefinitionName"), scopeText, scope, scopeName));
        }

        var events = new List<ActivityEventRow>();
        for (var i = 0; i < e.Count; i++)
        {
            var r = e[i];
            var caller = Str(r, "caller");
            var action = ValueOf(r, "operationName");
            var ts = Str(r, "eventTimestamp");
            if (caller is null || action is null || ts is null)
            {
                if (problems.Count < 8)
                    problems.Add($"{Path.GetFileName(activityPath)} event {i + 1} has no {string.Join(", ", new[] { caller is null ? "caller" : null, action is null ? "operationName" : null, ts is null ? "eventTimestamp" : null }.Where(x => x is not null))}");
                continue;
            }
            if (!DateTimeOffset.TryParse(ts, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var when))
            {
                if (problems.Count < 8) problems.Add($"{Path.GetFileName(activityPath)} event {i + 1}: eventTimestamp '{ts}' is not a date and time");
                continue;
            }
            events.Add(new ActivityEventRow(caller, action, when, Str(r, "resourceGroupName"), ValueOf(r, "status") ?? "Succeeded"));
        }

        var roles = new List<CatalogRole>();
        for (var i = 0; i < c.Count; i++)
        {
            var r = c[i];
            var props = r.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : r;   // Azure REST shape
            var id = Str(r, "id");
            var roleName = Str(props, "roleName");
            if (id is null || roleName is null || !props.TryGetProperty("permissions", out var perms) || perms.ValueKind != JsonValueKind.Array)
            {
                problems.Add($"{Path.GetFileName(catalogPath)} role {i + 1} has no {(id is null ? "id" : roleName is null ? "roleName" : "permissions list")}");
                continue;
            }
            var actions = new List<string>();
            var notActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var perm in perms.EnumerateArray())
            {
                actions.AddRange(Strings(perm, "actions"));
                foreach (var n in Strings(perm, "notActions")) notActions.Add(n);
            }
            roles.Add(new CatalogRole(id, roleName, actions.Where(x => !notActions.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
        }

        foreach (var ra in assignments.Where(ra => FindRole(roles, ra) is null))
            problems.Add($"the role of {ra.PrincipalName} ({ra.RoleDefinitionName ?? ra.RoleDefinitionId}) is not in {Path.GetFileName(catalogPath)}");

        if (problems.Count > 0) Fail("Missing fields check", string.Join("; ", problems.Take(5)) + (problems.Count > 5 ? $"; and {problems.Count - 5} more" : ""));
        steps.Add(new ValidationStep("Missing fields check", true, "principalId, roleDefinitionId and scope present in every record"));

        // ---- 3. Duplicate data check ----
        var dups = assignments.GroupBy(x => (x.PrincipalId.ToLowerInvariant(), x.RoleDefinitionId.ToLowerInvariant(), x.ScopeText.ToLowerInvariant()))
            .Where(g => g.Count() > 1).ToList();
        if (dups.Count > 0)
            Fail("Duplicate data check", $"{dups.Sum(g => g.Count() - 1)} duplicate role assignment(s) found, e.g. {dups[0].First().PrincipalName} ({dups[0].First().RoleDefinitionName ?? dups[0].First().RoleDefinitionId}) appears {dups[0].Count()} times");
        var twice = assignments.GroupBy(x => x.UserName.ToLowerInvariant()).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null)
            Fail("Duplicate data check", $"{twice.First().PrincipalName} has {twice.Count()} role assignments; SecureShift scores one role assignment per identity");
        steps.Add(new ValidationStep("Duplicate data check", true, "0 duplicate role assignments found"));

        // ---- 4. Activity coverage ----
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ra in assignments) { known.Add(ra.PrincipalId); known.Add(ra.PrincipalName); known.Add(ra.UserName); }
        bool InPeriod(ActivityEventRow ev) { var d = DateOnly.FromDateTime(ev.Timestamp.UtcDateTime); return d >= from && d <= to; }
        var usable = events.Where(ev => known.Contains(ev.Caller) && ev.Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) && InPeriod(ev)).ToList();
        var periodText = $"{Fmt.Date(from)} - {Fmt.Date(to)}";
        if (usable.Count == 0)
        {
            var range = events.Count == 0 ? "the file has no events at all"
                : $"the file has events from {Fmt.Date(DateOnly.FromDateTime(events.Min(x => x.Timestamp).UtcDateTime))} to {Fmt.Date(DateOnly.FromDateTime(events.Max(x => x.Timestamp).UtcDateTime))}; choose a period that covers them";
            Fail("Activity coverage", $"the Activity Log has no successful events of these identities between {periodText} ({range})");
        }
        steps.Add(new ValidationStep("Activity coverage", true, $"Activity Log covers {periodText} ({to.DayNumber - from.DayNumber + 1} days) for {companyName}"));

        var outside = events.Count(ev => !InPeriod(ev));
        var failed = events.Count(ev => InPeriod(ev) && !ev.Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase));
        var unknownCallers = events.Count(ev => InPeriod(ev) && ev.Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) && !known.Contains(ev.Caller));
        var summary = $"{assignments.Count} role assignments, {roles.Count} role definitions, {usable.Count} events used " +
                      $"({outside} outside the period, {failed} not successful, {unknownCallers} from unknown callers - ignored)";
        return new GovernanceInput(assignments, usable, roles, from, to, steps, summary);
    }

    /// <summary>The catalog role of an assignment: same id, same GUID at the end of the id, or the same role name.</summary>
    public static CatalogRole? FindRole(IReadOnlyList<CatalogRole> roles, RoleAssignmentRow ra)
    {
        static string Tail(string id) => id.TrimEnd('/').Split('/')[^1];
        return roles.FirstOrDefault(r => r.Id.Equals(ra.RoleDefinitionId, StringComparison.OrdinalIgnoreCase))
            ?? roles.FirstOrDefault(r => Tail(r.Id).Equals(Tail(ra.RoleDefinitionId), StringComparison.OrdinalIgnoreCase))
            ?? (ra.RoleDefinitionName is null ? null : roles.FirstOrDefault(r => r.RoleName.Equals(ra.RoleDefinitionName, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Azure scope → (Scope, ScopeName) as stored in dev."Identities":
    /// /subscriptions/x → Subscription; .../resourceGroups/rg-sales → Resource Group + rg-sales;
    /// .../providers/... → Resource + resource name; /providers/Microsoft.Management/managementGroups/mg → Management Group + mg.
    /// </summary>
    public static (string? Scope, string? Name) ParseScope(string scope)
    {
        var parts = scope.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && parts[0].Equals("providers", StringComparison.OrdinalIgnoreCase)
            && parts[1].Equals("Microsoft.Management", StringComparison.OrdinalIgnoreCase) && parts[2].Equals("managementGroups", StringComparison.OrdinalIgnoreCase))
            return ("Management Group", parts[3]);
        if (parts.Length < 2 || !parts[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)) return (null, null);
        if (parts.Length == 2) return ("Subscription", null);
        if (parts.Length == 4 && parts[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)) return ("Resource Group", parts[3]);
        if (parts.Length > 4 && parts[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)) return ("Resource", parts[^1]);
        return (null, null);
    }

    private static async Task<List<JsonElement>> ReadArrayAsync(string path, string what, Action<string> fail)
    {
        var name = Path.GetFileName(path);
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            fail($"{name} cannot be read: {ex.Message}");
            return [];
        }
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            fail($"{name} is not valid JSON (line {(ex.LineNumber ?? 0) + 1}, position {(ex.BytePositionInLine ?? 0) + 1}). Open it in an editor and fix it, or export it again.");
            return [];
        }
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var v)) root = v;   // Azure REST: {"value": [...]}
        if (root.ValueKind != JsonValueKind.Array)
        {
            fail($"{name} must be a JSON array of {what} (or an object with a \"value\" array), but it is a JSON {root.ValueKind.ToString().ToLowerInvariant()}");
            return [];
        }
        var list = root.EnumerateArray().ToList();
        var notObject = list.FindIndex(x => x.ValueKind != JsonValueKind.Object);
        if (notObject >= 0) fail($"{name}: item {notObject + 1} is not a JSON object");
        if (list.Count == 0) fail($"{name} is empty (no {what})");
        return list;
    }

    private static string? Str(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } s ? s.Trim() : null;
        return null;
    }

    /// <summary>"operationName": "x" or "operationName": {"value": "x"} (Azure uses the object form).</summary>
    private static string? ValueOf(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (!p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (p.Value.ValueKind == JsonValueKind.String) return p.Value.GetString() is { Length: > 0 } s ? s.Trim() : null;
            if (p.Value.ValueKind == JsonValueKind.Object) return Str(p.Value, "value");
        }
        return null;
    }

    private static IEnumerable<string> Strings(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array)
                foreach (var x in p.Value.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } s) yield return s.Trim();
    }
}
