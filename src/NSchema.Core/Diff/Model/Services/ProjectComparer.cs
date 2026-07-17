using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Model.Services;
using NSchema.Project.Model.Directives;
using NSchema.State.Model;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// Default <see cref="IProjectComparer"/>: composes the structural comparer with run-once resolution and
/// change-event matching, so the diff it produces is the complete current→desired difference.
/// </summary>
/// <param name="comparer">Produces the structural schema diff.</param>
internal sealed class ProjectComparer(IDatabaseComparer comparer) : IProjectComparer
{
    public Result<DatabaseDiff> Compare(CurrentState current, ProjectDefinition desired)
    {
        var diagnostics = new List<Diagnostic>();
        var allDirectives = desired.Directives;

        // Look up new scripts to run, and strip old ones from our directives.
        var deploymentScripts = GetNewScripts(allDirectives.DeploymentScripts, current.ExecutedScripts);
        diagnostics.AddRange(deploymentScripts.Diagnostics);
        var pendingDirectives = allDirectives with { DeploymentScripts = deploymentScripts.Require() };

        // Validate any RENAME directives relative to the rename.
        diagnostics.AddRange(ValidateRenameDirectives(current.Database, allDirectives));

        // The structural compare attaches each change-event script to the change it accompanies.
        // A change-event script whose change is not in the diff was not attached — a dead migration.
        var diff = comparer.Compare(current.Database, desired.Database, pendingDirectives);
        var attached = diff.ChangeScripts().ToHashSet();
        diagnostics.AddRange(allDirectives.ChangeScripts.Where(s => !attached.Contains(s)).Select(DeadMigrationDiagnostic));

        // Deployment scripts run at the bookends — they attach to no node, so they ride the diff root.
        return Result.From(diff with { DeploymentScripts = deploymentScripts.Require() }, diagnostics);
    }

    private static IEnumerable<Diagnostic> ValidateRenameDirectives(Database currentSchema, ProjectDirectives directives)
    {
        var current = new DatabaseLookup(currentSchema);

        foreach (var rename in directives.SchemaRenames)
        {
            if (current.FindSchema(rename.From) is null && current.FindSchema(rename.To) is not null)
            {
                yield return DiffDiagnostics.AppliedRename("schema", rename.From.Value, rename.To);
            }
        }

        foreach (var rename in directives.ObjectRenames)
        {
            if (!current.Has(rename.Kind, rename.From) && current.Has(rename.Kind, rename.From with { Name = rename.To }))
            {
                yield return DiffDiagnostics.AppliedRename(rename.Kind.Display(), rename.From.ToString(), rename.To);
            }
        }

        foreach (var rename in directives.MemberRenames)
        {
            if (!current.HasColumn(rename.From) && current.HasColumn(rename.From with { Member = rename.To }))
            {
                yield return DiffDiagnostics.AppliedRename("column", rename.From.ToString(), rename.To);
            }
        }
    }

    private static Result<List<DeploymentScript>> GetNewScripts(IReadOnlyCollection<DeploymentScript> desired, IReadOnlyCollection<ScriptExecution> current)
    {
        var diagnostics = new List<Diagnostic>();
        var newScripts = new List<DeploymentScript>();
        var executed = current.ToDictionary(s => s.Script, s => s.Hash);

        foreach (var script in desired)
        {
            if (script.RunCondition != RunCondition.Once || !executed.TryGetValue(script.Address, out var recordedHash))
            {
                newScripts.Add(script);
                continue;
            }

            if (!string.Equals(recordedHash, script.Hash, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(DiffDiagnostics.ChangedRunOnceScript(script));
            }
        }

        return Result.From(newScripts, diagnostics);
    }

    private static Diagnostic DeadMigrationDiagnostic(ChangeScript migration) => DiffDiagnostics.DeadMigration(migration);
}
