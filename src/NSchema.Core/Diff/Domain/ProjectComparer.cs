using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models;
using NSchema.Current.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Diff.Domain;

/// <summary>
/// Default <see cref="IProjectComparer"/>: composes the structural comparer with run-once resolution and
/// change-event matching, so the diff it produces is the complete current→desired difference.
/// </summary>
/// <param name="comparer">Produces the structural schema diff.</param>
internal sealed class ProjectComparer(ISchemaComparer comparer) : IProjectComparer
{
    public Result<DatabaseDiff> Compare(CurrentState current, ProjectDefinition desired)
    {
        var diagnostics = new List<Diagnostic>();

        // Resolve the run-once scripts first: an already-executed declaration is not part of the difference.
        var scripts = GetNewScripts(desired.Scripts, current.ExecutedScripts);
        diagnostics.AddRange(scripts.Diagnostics);

        // Diff the schemas, then attach the pending change-event scripts to their changes. An unmatched one's
        // event does not occur in this difference, so it will not run (and, if run-once, stays pending).
        var diff = comparer.Compare(current.Schema, desired.Schema);
        var annotated = MigrationAnnotator.Annotate(diff, [.. scripts.Require().Where(s => s.Event is ChangeEvent)]);
        diagnostics.AddRange(annotated.Unmatched.Select(DeadMigrationDiagnostic));

        // The diff's script list is every run this difference implies, in declaration order.
        var unmatched = annotated.Unmatched.ToHashSet();
        var pending = scripts.Require().Where(s => s.Event is DeploymentEvent || !unmatched.Contains(s)).ToList();

        return Result.From(annotated.Diff with { Scripts = pending }, diagnostics);
    }

    public DatabaseDiff CompareTeardown(DatabaseSchema currentSchema) => comparer.Compare(currentSchema, new DatabaseSchema());

    private static Result<List<Script>> GetNewScripts(IReadOnlyCollection<Script> desired, IReadOnlyCollection<ScriptExecution> current)
    {
        var diagnostics = new List<Diagnostic>();
        var newScripts = new List<Script>();
        var executed = current.ToDictionary(s => s.Name, s => s.Hash, StringComparer.OrdinalIgnoreCase);

        foreach (var script in desired)
        {
            if (script.RunCondition != RunCondition.Once || !executed.TryGetValue(script.Name, out var recordedHash))
            {
                newScripts.Add(script);
                continue;
            }

            if (!string.Equals(recordedHash, script.Hash, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic.Warning("run-once", $"Run-once script '{script.Name}' has changed since it was executed and stays skipped."));
            }
        }

        return Result.From(newScripts, diagnostics);
    }

    private static Diagnostic DeadMigrationDiagnostic(Script migration) => Diagnostic.Info(
        "data-migrations",
        $"Migration '{migration.Name}' ({migration.Event.Description}) matches " +
        "no change in this plan and will not run. If the change it supports has been applied everywhere, the block is safe to delete."
    );
}
