using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests.Phase5;

/// <summary>Architect and Executive menus driven by scripted keyboard input, against a fresh database.</summary>
public class Phase5MenuTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly string[] Logout = ["0", "y", "0"];

    [Fact]
    public async Task Architect_assesses_simulates_and_submits_then_the_Executive_approves()
    {
        var (_, arch) = await db.RunAppAsync([
            "1", "amanjeet.sharma@secureshift.io", "Architect@2026",
            "1",                           // dashboard
            "4", "2", "1", "2",            // assessments -> Customer360 -> run -> continue to simulation
            "2",                           // strategy 2 = Replatform
            "1", "", "Lift to managed .NET", "2", "y",   // create plan from it, keep the default name, notes, submit, confirm
            .. Logout]);
        Assert.Contains("Architect Dashboard", arch);
        Assert.Contains("Assessment ASM-006 saved: TD 47, GR 72", arch);
        Assert.Contains("Simulation SIM-007 complete: Medium risk", arch);
        Assert.Contains("Suggested order (most critical dependency first): Customer Database → Banking API → Customer360", arch);
        Assert.Contains("Total estimated effort: 16 = 16 weeks", arch);
        Assert.Contains("After submission, this plan will be locked and sent to the Executive for review.", arch);
        Assert.Contains("PLAN-007 submitted for approval", arch);

        var (_, exec) = await db.RunAppAsync([
            "1", "shivanand.kumatekar@secureshift.io", "Exec@2026",
            "1", "1", "1", "Go ahead", "y", "0",      // awaiting review -> first (PLAN-007, newest) -> approve
            "3",                                       // decision history
            .. Logout]);
        Assert.Contains("Plans awaiting review (4)", exec);
        Assert.Contains("This decision cannot be changed later.", exec);
        Assert.Contains("PLAN-007 approved", exec);
        Assert.Contains("Decision history", exec);

        await using var ctx = db.CreateContext();
        var plan = await ctx.Plans.AsNoTracking().Include(p => p.PlanPhases).SingleAsync(p => p.Code == "PLAN-007");
        Assert.Equal(("Customer360 Replatform Plan", "Approved", "Go ahead", "Lift to managed .NET"), (plan.PlanName, plan.Status, plan.ExecutiveComment, plan.Notes));
        Assert.Equal([2, 3, 7, 4], plan.PlanPhases.OrderBy(p => p.PhaseNo).Select(p => p.Weeks));
    }

}

/// <summary>Draft editing + rejection (own fresh database, so the plan counts do not depend on the test order).</summary>
public class Phase5DraftMenuTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly string[] Logout = ["0", "y", "0"];

    [Fact]
    public async Task Draft_plans_can_be_edited_and_submitted_and_rejected_plans_need_a_reason()
    {
        var (_, arch) = await db.RunAppAsync([
            "1", "amanjeet.sharma@secureshift.io", "Architect@2026",
            "7", "T", "Draft", "1", "2", "", "", "y", "0",   // My Plans -> Drafts -> PLAN-006 -> submit (keep name + notes)
            .. Logout]);
        Assert.Contains("Draft plan - you can edit the name and notes, then submit.", arch);
        Assert.Contains("PLAN-006 submitted for approval", arch);

        var (_, exec) = await db.RunAppAsync([
            "1", "shivanand.kumatekar@secureshift.io", "Exec@2026",
            "2", "T", "Submitted", "1", "2", "no", "Needs a pilot first", "y", "0",
            .. Logout]);
        Assert.Contains("[ERROR] Please give a reason (at least 5 characters) to reject", exec);
        Assert.Contains("rejected", exec);
        await using var ctx = db.CreateContext();
        Assert.Equal(1, await ctx.Plans.CountAsync(p => p.Status == "Rejected" && p.ExecutiveComment == "Needs a pilot first"));
    }
}
