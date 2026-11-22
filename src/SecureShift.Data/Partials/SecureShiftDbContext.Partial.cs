using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Models.Generated;

// Same namespace as the generated context, so this is the other half of the same partial class.
namespace SecureShift.Data.Context.Generated;

/// <summary>
/// Our additions to the scaffolded SecureShiftDbContext. The generated file in Context/Generated
/// is never edited; it calls OnModelCreatingPartial at the end of OnModelCreating.
/// </summary>
public partial class SecureShiftDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // EF Core leaves a column out of the INSERT when the value equals the CLR default (false for bool)
        // and the column has a database default. These four columns default to TRUE in the database,
        // so saving "false" would silently store TRUE. ValueGeneratedNever = always send our value.
        modelBuilder.Entity<User>().Property(e => e.MustChangePassword).ValueGeneratedNever();
        modelBuilder.Entity<GeneratedRole>().Property(e => e.IncludeBaselineRead).ValueGeneratedNever();
        modelBuilder.Entity<RuleSetting>().Property(e => e.IsActive).ValueGeneratedNever();
        modelBuilder.Entity<HighRiskPattern>().Property(e => e.IsEnabled).ValueGeneratedNever();
    }
}
