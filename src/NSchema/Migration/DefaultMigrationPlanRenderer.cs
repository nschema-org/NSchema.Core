using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationPlanRenderer"/>.
/// Acts as a facade over <see cref="IMigrationDiffBuilder"/> and <see cref="IMigrationDiffRenderer"/>
/// </summary>
internal sealed class DefaultMigrationPlanRenderer(IMigrationDiffBuilder builder, IMigrationDiffRenderer renderer) : IMigrationPlanRenderer
{
    public string Render(MigrationPlan plan)
    {
        var diff = builder.Build(plan);
        return renderer.Render(diff);
    }
}
