using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Migrations;
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

    public Result<PlannedMigration> Plan(DatabaseSchema currentSchema, DesiredProject desired)
    {
        // Validate the desired schema. A schema-policy error is fatal and skips the rest — there is no plan to carry.
        var schemaDiagnostics = Validate(desired.Schema);
        if (schemaDiagnostics.HasErrors)
        {
            return Result.Failure<PlannedMigration>(schemaDiagnostics);
        }

        // Diff the schemas, then attach the declared data migrations.
        var diff = comparer.Compare(currentSchema, desired.Schema);
        var (annotated, unmatched) = MigrationMatcher.Apply(diff, desired.Migrations);
        diff = annotated;

        // Validate the diff.
        var diagnostics = new List<Diagnostic>(schemaDiagnostics);
        diagnostics.AddRange(diffPolicies.SelectMany(p => p.Validate(diff)));
        diagnostics.AddRange(unmatched.Select(DeadMigrationDiagnostic));

        // Convert the diff into a migration plan.
        var actions = linearizer.Linearize(diff);
        var preDeploymentScripts = desired.Scripts.Where(s => s.Type == ScriptType.PreDeployment).ToList();
        var postDeploymentScripts = desired.Scripts.Where(s => s.Type == ScriptType.PostDeployment).ToList();
        var plan = new MigrationPlan(actions, preDeploymentScripts, postDeploymentScripts);

        return Result.From(new PlannedMigration(diff, plan), diagnostics);
    }

    private static Diagnostic DeadMigrationDiagnostic(DataMigration migration)
    {
        var label = migration.Name is { } name
            ? $"Migration '{name}' ({DataMigration.TriggerText(migration.Trigger)} {migration.Path})"
            : $"Migration for {DataMigration.TriggerText(migration.Trigger)} {migration.Path}";
        return Diagnostic.Info("data-migrations",
            $"{label} matches no change in this plan and will not run. " +
            "If the change it supports has been applied everywhere, the block is safe to delete.");
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
