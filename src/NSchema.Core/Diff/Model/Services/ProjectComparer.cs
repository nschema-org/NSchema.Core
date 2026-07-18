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
        var diagnostics = new List<Diagnostic>();
        var directives = desired.Directives;

        // Look up new scripts to run, excluding those already executed.
        var deploymentScripts = GetNewScripts(directives.DeploymentScripts, current.ExecutedScripts);
        diagnostics.AddRange(deploymentScripts.Diagnostics);

        // Align: rewrite the current schema into the declared name-space.
        var aligned = DatabaseAligner.Align(current.Database, desired.Database, directives);
        diagnostics.AddRange(aligned.Diagnostics);

        // Compare: the structural diff.
        var diff = comparer.Compare(aligned.Require(), desired.Database);

        // Decorate: attach each change-event script to the change it accompanies.
        var decorate = ChangeScriptDecorator.Decorate(diff, directives.ChangeScripts);
        diagnostics.AddRange(decorate.Diagnostics);
        diff = decorate.Require();

        // Deployment scripts run at the bookends — they attach to no node, so they ride the diff root.
        return Result.From(diff with { DeploymentScripts = deploymentScripts.Require() }, diagnostics);
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
}
