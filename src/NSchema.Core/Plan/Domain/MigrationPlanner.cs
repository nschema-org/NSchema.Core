using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Policies;
using NSchema.Plan.Backends;
using NSchema.Plan.Domain.Models;
using NSchema.Project.Domain.Models;
using NSchema.Project.Policies;

namespace NSchema.Plan.Domain;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>.
/// </summary>
/// <param name="comparer">Produces the complete diff from the current state and the desired project.</param>
/// <param name="linearizer">Derives the ordered actions from the diff, weaving its scripts in.</param>
/// <param name="schemaPolicies">Policies that validate the desired schema.</param>
/// <param name="diffPolicies">Policies that validate the diff (e.g. destructive-change checks).</param>
/// <param name="dialect">The SQL dialect the plan's statements are rendered with. Required for planning.</param>
internal sealed class MigrationPlanner(
    IProjectComparer comparer,
    IPlanLinearizer linearizer,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IDiffPolicy> diffPolicies,
    ISqlDialect? dialect = null
) : IMigrationPlanner
{
    public Result Validate(DatabaseSchema desiredSchema) =>
        Result.From(schemaPolicies.SelectMany(p => p.Validate(desiredSchema)));

    public Result<MigrationPlan> Plan(CurrentState current, ProjectDefinition desired)
    {
        if (dialect is null)
        {
            return MissingDialect();
        }

        var diagnostics = new List<Diagnostic>();

        // Validate the desired schema.
        var schemaValidation = Validate(desired.Schema);
        diagnostics.AddRange(schemaValidation.Diagnostics);

        // Compare it with the current state.
        var compared = comparer.Compare(current, desired);
        diagnostics.AddRange(compared.Diagnostics);
        var diff = compared.Require();

        // Validate the diff.
        diagnostics.AddRange(diffPolicies.SelectMany(p => p.Validate(diff)));

        var plan = Realize(diff, dialect);
        return Result.From(plan, diagnostics);
    }

    public Result<MigrationPlan> PlanTeardown(DatabaseSchema currentSchema)
    {
        if (dialect is null)
        {
            return MissingDialect();
        }

        // Don't run policies for teardown. This is a purely destructive action, and needs to be available as an escape.
        var diff = comparer.CompareTeardown(currentSchema);
        var plan = Realize(diff, dialect);
        return Result.Success(plan);
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

    private static Result<MigrationPlan> MissingDialect() => Result.Failure<MigrationPlan>(Diagnostic.Error(
        "plan", "Planning requires a database provider to render SQL, but none is registered."));
}
