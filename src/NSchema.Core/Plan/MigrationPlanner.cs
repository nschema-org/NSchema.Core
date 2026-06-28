using NSchema.Diff;
using NSchema.Diagnostics;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Plan;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>.
/// </summary>
/// <param name="comparer">Produces the structured diff by comparing the two schemas.</param>
/// <param name="linearizer">Derives the ordered executable plan from the diff.</param>
/// <param name="schemaPolicies">Policies that validate the desired schema.</param>
/// <param name="diffPolicies">Policies that validate the structured diff (e.g. destructive-change checks).</param>
internal sealed class MigrationPlanner(
    ISchemaComparer comparer,
    IPlanLinearizer linearizer,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IDiffPolicy> diffPolicies
) : IMigrationPlanner
{
    public PolicyDiagnostics Validate(DatabaseSchema desiredSchema) =>
        new(schemaPolicies.SelectMany(p => p.Validate(desiredSchema)));

    public Result<PlannedMigration> Plan(DatabaseSchema currentSchema, DatabaseSchema desiredSchema, IReadOnlyList<Script> scripts)
    {
        // Validate the desired schema. A schema-policy error is fatal and skips the rest — there is no plan to carry.
        var schemaDiagnostics = Validate(desiredSchema);
        if (schemaDiagnostics.HasErrors)
        {
            return Result.Failure<PlannedMigration>(schemaDiagnostics);
        }

        // Diff the schemas.
        var diff = comparer.Compare(currentSchema, desiredSchema);

        // Validate the diff.
        var diagnostics = new List<Diagnostic>(schemaDiagnostics);
        diagnostics.AddRange(diffPolicies.SelectMany(p => p.Validate(diff)));

        // Convert the diff into a migration plan.
        var actions = linearizer.Linearize(diff);
        var preDeploymentScripts = scripts.Where(s => s.Type == ScriptType.PreDeployment).ToList();
        var postDeploymentScripts = scripts.Where(s => s.Type == ScriptType.PostDeployment).ToList();
        var plan = new MigrationPlan(actions, preDeploymentScripts, postDeploymentScripts);

        return Result.From(new PlannedMigration(diff, plan), diagnostics);
    }

    public Result<PlannedMigration> PlanTeardown(DatabaseSchema currentSchema)
    {
        // Don't run transformers/policies for teardown.
        // This is a purely destructive action, and needs to be available as an escape.
        var diff = comparer.Compare(currentSchema, new DatabaseSchema());
        var actions = linearizer.Linearize(diff);
        var plan = new MigrationPlan(actions, [], []);
        return Result.Success(new PlannedMigration(diff, plan));
    }
}
