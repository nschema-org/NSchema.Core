using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Scripts.Model;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Operations;

/// <summary>
/// Default <see cref="IOperationReporter"/> that presents user-facing output.
/// </summary>
internal sealed class DefaultOperationReporter : IOperationReporter
{
    public const string ReporterName = "default";

    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly IDiffRenderer _diffRenderer;
    private readonly ISqlPlanRenderer _sqlPlanRenderer;

    /// <summary>
    /// Default <see cref="IOperationReporter"/> that presents user-facing output.
    /// </summary>
    /// <param name="diffRenderer">Renders the migration diff as human-readable text.</param>
    /// <param name="sqlPlanRenderer">Renders the SQL plan as human-readable text.</param>
    public DefaultOperationReporter(IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer)
        : this(diffRenderer, sqlPlanRenderer, Console.Out, Console.Error) { }

    /// <summary>
    /// Default <see cref="IOperationReporter"/> that presents user-facing output.
    /// </summary>
    /// <param name="diffRenderer">Renders the migration diff as human-readable text.</param>
    /// <param name="sqlPlanRenderer">Renders the SQL plan as human-readable text.</param>
    /// <param name="output">The writer for informational output (typically stdout).</param>
    /// <param name="error">The writer for errors and warnings (typically stderr).</param>
    public DefaultOperationReporter(IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer, TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
        _diffRenderer = diffRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
    }

    public void Info(string message) => _output.WriteLine(message);

    public void ReportException(Exception exception)
    {
        if (exception is PolicyViolationException pve)
        {
            _error.WriteLine(pve.Message);
            ReportDiagnosticItems(pve.Errors, _error);
        }
        else
        {
            _error.WriteLine($"Operation failed: {exception.Message}");
        }
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        var render = _diffRenderer.Render(diff);
        _output.WriteLine(render);
        _output.WriteLine();
    }

    public void ReportPlan(MigrationPlan plan)
    {
        ReportScripts("Pre-deployment scripts:", plan.PreDeploymentScripts);
        ReportScripts("Post-deployment scripts:", plan.PostDeploymentScripts);
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        var render = _sqlPlanRenderer.Render(plan);
        _output.WriteLine(render);
        _output.WriteLine();
    }

    private void ReportScripts(string heading, IReadOnlyList<Script> scripts)
    {
        if (scripts.Count == 0)
        {
            return;
        }

        _output.WriteLine(heading);
        foreach (var script in scripts)
        {
            _output.WriteLine($"  - {script.Name}");
        }
        _output.WriteLine();
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        _output.WriteLine("Policy diagnostics:");
        ReportDiagnosticItems(diagnostics, _output);
    }

    private void ReportDiagnosticItems(IReadOnlyList<PolicyDiagnostic> diagnostics, TextWriter pipe)
    {
        if (diagnostics.Count == 0)
        {
            pipe.WriteLine("- Nothing to report");
            pipe.WriteLine();
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            pipe.WriteLine($"- {diagnostic.PolicyName}: {diagnostic.Message}");
        }
        pipe.WriteLine();
    }
}
