using NSchema.Diff.Model;
using NSchema.Diff.Model.Services;
using NSchema.Model;
using NSchema.Plan.Backends;
using NSchema.Plan.Policies;
using NSchema.Project.Model.Directives;
using NSchema.Project.Policies;

namespace NSchema.Plan.Model.Services;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>.
/// </summary>
/// <param name="comparer">Produces the complete diff from the current state and the desired project.</param>
/// <param name="linearizer">Derives the ordered actions from the diff, weaving its scripts in.</param>
/// <param name="projectPolicies">Policies that validate the declared project.</param>
/// <param name="planPolicies">Policies that validate the complete plan (e.g. destructive-change checks).</param>
/// <param name="dialect">The SQL dialect the plan's statements are rendered with. Required for planning.</param>
internal sealed class MigrationPlanner(
    IProjectComparer comparer,
    IPlanLinearizer linearizer,
    IEnumerable<IProjectPolicy> projectPolicies,
    IEnumerable<IPlanPolicy> planPolicies,
    ISqlDialect? dialect = null
) : IMigrationPlanner
{
    public Result Validate(ProjectDefinition desired) =>
        Result.From(projectPolicies.SelectMany(p => p.Validate(desired)));

    public Result<MigrationPlan> Plan(CurrentState current, ProjectDefinition desired, PlanningScope scope)
    {
        if (dialect is null)
        {
            return Result.Failure<MigrationPlan>(PlanDiagnostics.MissingDialect);
        }

        var diagnostics = new List<Diagnostic>();

        // Validate the declared project.
        diagnostics.AddRange(Validate(desired).Diagnostics);

        // Compare it with the current state.
        var compared = comparer.Compare(current, desired);
        diagnostics.AddRange(compared.Diagnostics);

        // Compute and scope the resulting diff.
        var scopeResult = compared.Require().ScopedTo(scope, current.Database);
        diagnostics.AddRange(scopeResult.Diagnostics);
        var diff = scopeResult.Require();

        var plan = Realize(diff, dialect);

        // Validate the complete plan — post-render, so policies see exactly what an apply would execute.
        diagnostics.AddRange(planPolicies.SelectMany(p => p.Validate(plan)));

        return Result.From(plan, diagnostics);
    }

    /// <summary>
    /// Constructs an executable plan from a diff.
    /// </summary>
    private MigrationPlan Realize(DatabaseDiff diff, ISqlDialect sql)
    {
        var planStatements = new List<SqlStatement>();
        foreach (var action in linearizer.Linearize(diff))
        {
            var actionStatements = sql.Generate(action);
            planStatements.AddRange(actionStatements);
        }

        return new MigrationPlan(diff, planStatements);
    }
}
