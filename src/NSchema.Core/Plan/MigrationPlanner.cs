using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;

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

    public Result<PlannedMigration> Plan(CurrentState current, DesiredProject desired)
    {
        // Validate the desired schema. A schema-policy error is fatal and skips the rest — there is no plan to carry.
        var schemaDiagnostics = Validate(desired.Schema);
        if (schemaDiagnostics.HasErrors)
        {
            return Result.Failure<PlannedMigration>(schemaDiagnostics);
        }

        var diagnostics = new List<Diagnostic>(schemaDiagnostics);

        // Plan the scripts, skipping scripts we've already run.
        var executed = current.ExecutedScripts.ToDictionary(s => s.Name, s => s.Hash, StringComparer.OrdinalIgnoreCase);
        var scripts = desired.Scripts.Where(s => !IsAlreadyRun(s, executed, diagnostics)).ToList();
        var migrations = desired.Migrations.Where(m => !IsAlreadyRun(m, executed, diagnostics)).ToList();

        // Diff the schemas, then attach the declared data migrations.
        var diff = comparer.Compare(current.Schema, desired.Schema);
        var (annotated, unmatched) = MigrationMatcher.Apply(diff, migrations);
        diff = annotated;

        // Validate the diff.
        diagnostics.AddRange(diffPolicies.SelectMany(p => p.Validate(diff)));
        diagnostics.AddRange(unmatched.Select(DeadMigrationDiagnostic));

        // Convert the diff into a migration plan.
        var actions = linearizer.Linearize(diff);
        var preDeploymentScripts = scripts.Where(s => s.Type == ScriptType.PreDeployment).ToList();
        var postDeploymentScripts = scripts.Where(s => s.Type == ScriptType.PostDeployment).ToList();
        var plan = new MigrationPlan(actions, preDeploymentScripts, postDeploymentScripts);

        // The run-once work this plan will actually execute: every pending run-once bookend script (they always
        // run), and the pending run-once migrations whose change is in the plan (an unmatched one's event never
        // occurred, so it stays pending).
        var unmatchedNames = new HashSet<string>(unmatched.Where(m => m.Name is not null).Select(m => m.Name!), StringComparer.OrdinalIgnoreCase);
        var runOnce = scripts
            .Where(s => s.RunCondition == RunCondition.Once)
            .Select(s => new ScriptHash(s.Name, s.Hash))
            .Concat(migrations
                .Where(s => s.RunCondition == RunCondition.Once && !unmatchedNames.Contains(s.Name!))
                .Select(s => new ScriptHash(s.Name!, s.Hash)))
            .ToList();

        return Result.From(new PlannedMigration(diff, plan) { Scripts = runOnce }, diagnostics);
    }

    /// <summary>
    /// Whether a script has already run, adding the skip diagnostic when it has.
    /// </summary>
    private static bool IsAlreadyRun(IScriptDeclaration script, Dictionary<string, string> executed, List<Diagnostic> diagnostics)
    {
        if (script.RunCondition != RunCondition.Once || !executed.TryGetValue(script.Name!, out var recordedHash))
        {
            return false;
        }

        if (!string.Equals(recordedHash, script.Hash, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Diagnostic.Warning("run-once", $"Run-once script '{script.Name}' has changed since it was executed and stays skipped."));
        }

        return true;
    }

    private static Diagnostic DeadMigrationDiagnostic(DataMigration migration)
    {
        var label = migration.Name is { } name
            ? $"Migration '{name}' ({DataMigration.TriggerText(migration.Trigger)} {migration.Path})"
            : $"Migration for {DataMigration.TriggerText(migration.Trigger)} {migration.Path}";
        return Diagnostic.Info(
            "data-migrations",
            $"{label} matches no change in this plan and will not run. If the change it supports has been applied everywhere, the block is safe to delete."
        );
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
