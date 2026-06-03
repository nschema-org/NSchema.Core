using NSchema.Migration.Plan;

namespace NSchema.Migration.Diff;

/// <summary>
/// Default <see cref="IMigrationPlanRenderer"/>.
/// Acts as a facade over <see cref="IDiffBuilder"/> and <see cref="IDiffRenderer"/>
/// </summary>
internal sealed class DefaultMigrationPlanRenderer(IDiffBuilder builder, IDiffRenderer renderer) : IMigrationPlanRenderer
{
    public string Render(MigrationPlan plan)
    {
        var diff = builder.Build(plan);
        return renderer.Render(diff);
    }
}
