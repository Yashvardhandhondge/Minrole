using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests.Phase6;

/// <summary>
/// The whole SecureShift workflow in one test, one database, checking the database after every step
/// (the same flow as tests/e2e/phase6-full-workflow.sh, here through scripted IConsoleIO input).
/// </summary>
public class FullWorkflowTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly string[] Logout = ["0", "y", "0"];

    [Fact]
    public async Task Super_Admin_Admin_Architect_Executive_Cloud_Admin()
    {
        // 1. Super Admin adds an Architect
        var (_, o1) = await db.RunAppAsync(["1", "arya.mujumdar@secureshift.io", "Super@2026", "2", "A", "Nisha Verma", "nisha.verma@company.com", "9811223344", "3", "0", .. Logout]);
        var temp = Regex.Match(o1, "Temporary password : (Temp@[0-9]{4}x)").Groups[1].Value;
        await using (var ctx = db.CreateContext())
        {
            var u = await ctx.Users.AsNoTracking().Include(x => x.Role).SingleAsync(x => x.Email == "nisha.verma@company.com");
            Assert.Equal(("architect", "Active", true), (u.Role.RoleCode, u.Status, u.MustChangePassword));
        }

        // 2. Admin registers + classifies an enterprise, adds an application and a dependency
        var (_, o2) = await db.RunAppAsync([
            "1", "sachin.kumar@secureshift.io", "Admin@2026",
            "2", "Tailspin Airlines", "8", "Pune", "Kavita Desai", "kavita.desai@tailspin.com", "9812300011",
            "5", "12", "20", "8", "250000", "3", "400", "2", "1", "2", "30", "1,3", "2,4", "3", "4", "3", "2",
            "6", "R", "Flight Booking", "Sales", "Web Team", "1", "3", "1", "Java 17", "1", "6", "1", "2", "300000", "4", "1", "1", "1", "1", "0",
            "7", "A", "2", "1", "Booking Database", "L", "1", "2", "4", "Stores bookings", "0",
            .. Logout]);
        Assert.Contains("[OK] EMSI 32.33 - Growth category", o2);
        await using (var ctx = db.CreateContext())
        {
            var app = await ctx.Applications.AsNoTracking().SingleAsync(a => a.Code == "APP-011");
            Assert.Equal(("Flight Booking", "Critical"), (app.Name, app.Criticality));
            Assert.Equal(1, await ctx.Dependencies.CountAsync(d => d.SourceApplicationId == app.ApplicationId));
            Assert.Equal(32.33m, (await ctx.Classifications.SingleAsync(c => c.EnterpriseId == 4)).EmsiScore);
        }

        // 3. The new Architect: forced password change, assess, simulate (Refactor), create + submit the plan
        var (_, o3) = await db.RunAppAsync([
            "1", "nisha.verma@company.com", temp, temp, "Nisha@2026x", "Nisha@2026x",
            "2", "4", "4", "11", "1", "2", "3", "1", "", "First Tailspin plan", "2", "y",
            .. Logout]);
        Assert.Contains("PLAN-007 submitted for approval", o3);
        await using (var ctx = db.CreateContext())
        {
            var plan = await ctx.Plans.AsNoTracking().Include(p => p.Simulation).Include(p => p.PlanPhases).Include(p => p.CreatedByNavigation).SingleAsync(p => p.Code == "PLAN-007");
            Assert.Equal(("Submitted", "Refactor", "Nisha Verma"), (plan.Status, plan.Simulation.Strategy, plan.CreatedByNavigation.FullName));
            Assert.Equal(plan.Simulation.EffortWeeks, plan.PlanPhases.Sum(p => p.Weeks));
        }

        // 4. Executive approves
        var (_, o4) = await db.RunAppAsync(["1", "shivanand.kumatekar@secureshift.io", "Exec@2026", "1", "1", "1", "Approved for Tailspin", "y", "0", .. Logout]);
        Assert.Contains("PLAN-007 approved", o4);
        await using (var ctx = db.CreateContext())
            Assert.Equal("Approved", (await ctx.Plans.SingleAsync(p => p.Code == "PLAN-007")).Status);

        // 5. Cloud Admin runs an analysis from the sample JSON files
        var (_, o5) = await db.RunAppAsync(["1", "aarav.mehta@secureshift.io", "Cloud@2026", "3", "1", "4", "08/12/2026", "11/09/2026",
            Samples.Assignments, Samples.Activity, Samples.Catalog, "y", .. Logout]);
        Assert.Contains("Analysis Wizard - Step 4 of 4: Results", o5);
        await using (var ctx = db.CreateContext())
        {
            var a = await ctx.Analyses.AsNoTracking().OrderByDescending(x => x.AnalysisId).FirstAsync();
            Assert.Equal((20, "Complete"), (a.IdentityCount, a.Status));
            Assert.Equal(
                ["User Created", "Enterprise Registered", "Enterprise Classified", "Application Registered", "Component Added", "Dependency Added",
                 "Password Changed", "Assessment Run", "Simulation Run", "Plan Submitted", "Plan Approved", "Analysis Run"],
                await ctx.AuditLogs.Where(x => x.AuditLogId > 8 && x.Action != "Logged In" && x.Action != "Logged Out").OrderBy(x => x.AuditLogId).Select(x => x.Action).ToListAsync());
        }
        foreach (var o in new[] { o1, o2, o3, o4, o5 }) Assert.DoesNotContain("Exception", o);
    }
}
