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
    SqlDialect? dialect = null
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

        var diagnostics = new DiagnosticCollector();

        // Validate the declared project.
        diagnostics.Add(Validate(desired));

        // The plan converges what NSchema manages: the current side is the observation restricted to the
        // managed identities plus everything the project declares or addresses.
        var visible = current.FilteredTo(ManagementFilter(current, desired));

        // Compare it with the current state.
        var compared = diagnostics.Require(comparer.Compare(visible, desired));

        // Compute and scope the resulting diff. Widening consults the full observation:
        // an unmanaged dependency would still physically block a drop, so we need to warn on it.
        var diff = diagnostics.Require(compared.ScopedTo(scope, current.Database));

        var plan = Realize(diff, dialect, ManagedAfterApply(current, desired, scope), diagnostics);

        // Validate the complete plan — post-render, so policies see exactly what an apply would execute.
        diagnostics.Add(planPolicies.SelectMany(p => p.Validate(plan)));

        return diagnostics.ToResult(plan);
    }

    /// <summary>
    /// The current-side visibility filter: the managed identities, everything the project declares (also under
    /// a renamed schema's current name), and every rename directive's source — renames address current reality,
    /// so their sources are under management too.
    /// </summary>
    private static IdentitySet ManagementFilter(CurrentState current, ProjectDefinition desired)
    {
        var declared = desired.Database.Identities();
        var directives = desired.Directives;

        // Objects declared inside a renamed schema exist under the schema's current name until the rename applies.
        var currentSchemaNames = directives.SchemaRenames.ToDictionary(r => r.To, r => r.From);
        var declaredUnderCurrentNames = declared.Objects
            .Where(o => currentSchemaNames.ContainsKey(o.Schema))
            .Select(o => o with { Schema = currentSchemaNames[o.Schema] });

        var renameSources = new IdentitySet(
            [.. directives.SchemaRenames.Select(r => r.From)],
            [.. directives.ObjectRenames.Select(r => r.From), .. declaredUnderCurrentNames]);

        return current.Managed.Union(declared).Union(renameSources);
    }

    /// <summary>
    /// The managed identities an apply of this plan establishes. Within the plan's scope, management after an
    /// apply is exactly what the project declares there — created, adopted, renamed, or gone; outside it,
    /// whatever was already managed stays managed. Extensions are database-global, so they are always in scope.
    /// </summary>
    private static IdentitySet ManagedAfterApply(CurrentState current, ProjectDefinition desired, PlanningScope scope)
    {
        var retained = current.Managed.Except(current.Managed.CoveredBy(scope));
        return desired.ScopedTo(scope).Database.Identities().Union(retained);
    }

    /// <summary>
    /// Constructs an executable plan from a diff. A rendering's diagnostics ride the plan result — an
    /// unsupported action is an error that blocks application, not a hole in the statement list silently.
    /// </summary>
    private MigrationPlan Realize(DatabaseDiff diff, SqlDialect sql, IdentitySet managed, DiagnosticCollector diagnostics)
    {
        var planStatements = new List<SqlStatement>();
        foreach (var action in linearizer.Linearize(diff))
        {
            if (diagnostics.TryTake(sql.Generate(action), out var actionStatements))
            {
                planStatements.AddRange(actionStatements);
            }
        }

        return new MigrationPlan(diff, planStatements, managed);
    }
}
