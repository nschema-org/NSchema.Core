using NSchema.Model.Scripts;
using NSchema.Project.Model.Directives;
using NSchema.State.Model;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// Default <see cref="IProjectComparer"/>. Produces a diff of the current→desired states.
/// </summary>
/// <param name="comparer">For performing structural database diff.</param>
internal sealed class ProjectComparer(IDatabaseComparer comparer) : IProjectComparer
{
    public Result<DatabaseDiff> Compare(CurrentState current, ProjectDefinition desired)
    {
        var diagnostics = new DiagnosticCollector();
        var directives = desired.Directives;

        // Look up new scripts to run, excluding those already executed.
        var deploymentScripts = diagnostics.Require(GetNewScripts(directives.DeploymentScripts, current.ExecutedScripts));

        // Align: rewrite the current schema into the declared name-space.
        var aligned = diagnostics.Require(DatabaseAligner.Align(current.Database, desired.Database, directives));

        // Compare: the structural diff.
        var diff = comparer.Compare(aligned, desired.Database);

        // Decorate: attach each change-event script to the change it accompanies.
        diff = diagnostics.Require(ChangeScriptDecorator.Decorate(diff, directives.ChangeScripts));

        // Deployment scripts run at the bookends — they attach to no node, so they ride the diff root.
        return diagnostics.ToResult(diff with { DeploymentScripts = deploymentScripts });
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

            if (recordedHash != script.Hash)
            {
                diagnostics.Add(DiffDiagnostics.ChangedRunOnceScript(script));
            }
        }

        return Result.From(newScripts, diagnostics);
    }
}
