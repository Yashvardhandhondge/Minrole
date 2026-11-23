using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Governance;
using SecureShift.ConsoleApp.Services.Modernization;

namespace SecureShift.Tests.Phase6;

/// <summary>App:TimeZone (India): times on screen and date-only columns use the India date, also at 01:00 IST.</summary>
public class TimeZoneUnitTests
{
    // 2026-11-22 19:30 UTC = 2026-11-23 01:00 IST (UTC+05:30): already the next day in India
    private static readonly DateTime OneAmIst = new(2026, 11, 22, 19, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void The_configured_zone_is_India()
    {
        Assert.Equal(TimeSpan.FromHours(5.5), AppTime.Zone.BaseUtcOffset);
        Assert.Equal(TimeSpan.FromHours(5.5), AppTime.Resolve("Asia/Kolkata").BaseUtcOffset);
        Assert.Equal(TimeSpan.FromHours(5.5), AppTime.Resolve("India Standard Time").BaseUtcOffset);
    }

    [Fact]
    public void Falls_back_to_India_Standard_Time_when_the_IANA_id_is_not_found()
    {
        var india = TimeZoneInfo.CreateCustomTimeZone("India Standard Time", TimeSpan.FromHours(5.5), "India Standard Time", "India Standard Time");
        TimeZoneInfo? OldWindows(string id) => id == "India Standard Time" ? india : null;   // a Windows without IANA ids
        Assert.Same(india, AppTime.Resolve("Asia/Kolkata", OldWindows));
        Assert.Same(TimeZoneInfo.Utc, AppTime.Resolve("Nowhere/Nothing", _ => null));         // nothing at all: UTC, never a crash
    }

    [Fact]
    public void At_01_00_IST_the_date_is_already_the_next_day()
    {
        Assert.Equal(new DateOnly(2026, 11, 23), AppTime.DateOf(OneAmIst));
        Assert.Equal("11/23/2026 01:00 AM", Fmt.DateTime(OneAmIst));
        Assert.Equal("11/23/2026", Fmt.DateOf(OneAmIst));
        Assert.Equal(new DateOnly(2026, 11, 22), AppTime.DateOf(new DateTime(2026, 11, 22, 18, 29, 0, DateTimeKind.Utc)));   // 23:59 IST
        Assert.Equal(new DateTime(2026, 11, 22, 18, 30, 0, DateTimeKind.Utc), AppTime.StartOfDayUtc(new DateOnly(2026, 11, 23)));
    }
}

/// <summary>Date-only columns written by the services use the India date (fresh database).</summary>
public class TimeZoneDbTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly FixedClock OneAmIst = new(new DateTimeOffset(2026, 11, 22, 19, 30, 0, TimeSpan.Zero));

    private T Get<T>() where T : notnull => db.BuildServices(clock: OneAmIst).GetRequiredService<T>();

    [Fact]
    public async Task Plans_decisions_assessments_and_reports_get_the_India_date_at_01_00_IST()
    {
        var planning = Get<PlanningService>();
        var nov23 = new DateOnly(2026, 11, 23);

        var asm = await planning.RunAssessmentAsync(2, 13);
        Assert.Equal(nov23, asm.AssessedAt);
        var sim = await planning.RunSimulationAsync(2, "Rehost", 13);
        Assert.Equal(nov23, sim.CreatedAt);
        var plan = await planning.CreatePlanAsync(sim.SimulationId, "Customer360 Rehost Plan", null, submit: true, 13);
        Assert.Equal((nov23, (DateOnly?)nov23), (plan.CreatedAt, plan.SubmittedAt));
        var decided = await planning.DecideAsync(plan.PlanId, approve: true, null, 14);
        Assert.Equal(nov23, decided.DecidedAt);

        var report = await Get<GovernanceService>().GenerateReportAsync(1, 25);
        Assert.Equal("11/23/2026", Fmt.DateOf(report.GeneratedAt));
    }

    [Fact]
    public async Task Audit_log_date_filter_uses_India_days()
    {
        await db.SqlAsync("""
            INSERT INTO dev."AuditLogs" ("UserId", "Action", "Target", "Detail", "CreatedAt") VALUES
              (1, 'Tz Probe', 'late evening', '23:30 IST on 11/22', '2026-11-22T18:00:00Z'),
              (1, 'Tz Probe', 'one am', '01:00 IST on 11/23', '2026-11-22T19:30:00Z')
            """);
        var page = await Get<AuditLogService>().QueryAsync(new AuditFilter(Action: "Tz Probe", From: new DateOnly(2026, 11, 23), To: new DateOnly(2026, 11, 23)), 1);
        Assert.Equal("one am", Assert.Single(page.Items).Target);
    }
}

/// <summary>Item 1: the Super Admin dashboard hides sign-in rows; the Audit Logs page keeps them.</summary>
public class DashboardSignInRowsTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    [Fact]
    public async Task Recent_activity_hides_login_logout_failed_and_blocked_rows()
    {
        var auth = db.Get<AuthService>();
        await auth.LoginAsync("arya.mujumdar@secureshift.io", "Super@2026");          // Logged In
        await auth.LoginAsync("arya.mujumdar@secureshift.io", "Wrong@2026");          // Login Failed
        await auth.LoginAsync("sakshi.patil@company.com", "Welcome@2026");            // Login Blocked
        await auth.LogoutAsync(new Session(1, RoleCodes.SuperAdmin, DateTime.UtcNow)); // Logged Out

        var d = await db.Get<UserAdminService>().DashboardAsync();
        Assert.Equal(5, d.RecentActivity.Count);
        Assert.DoesNotContain(d.RecentActivity, a => UserAdminService.SignInActions.Contains(a.Action));
        Assert.Equal("User Created", d.RecentActivity[0].Action);   // the newest NON sign-in row

        var audit = db.Get<AuditLogService>();
        foreach (var action in UserAdminService.SignInActions)
            Assert.NotEmpty((await audit.QueryAsync(new AuditFilter(Action: action), 1)).Items);
        await using var ctx = db.CreateContext();
        Assert.Equal(4, await ctx.AuditLogs.CountAsync(a => UserAdminService.SignInActions.Contains(a.Action)));
    }
}
