using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationReporter"/> that presents user-facing output.
/// </summary>
internal sealed class DefaultMigrationReporter : IMigrationReporter
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly IDiffRenderer _diffRenderer;
    private readonly ISqlPlanRenderer _sqlPlanRenderer;

    /// <summary>
    /// Default <see cref="IMigrationReporter"/> that presents user-facing output.
    /// </summary>
    /// <param name="diffRenderer">Renders the migration diff as human-readable text.</param>
    /// <param name="sqlPlanRenderer">Renders the SQL plan as human-readable text.</param>
    public DefaultMigrationReporter(IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer)
        : this(diffRenderer, sqlPlanRenderer, Console.Out, Console.Error) { }

    /// <summary>
    /// Default <see cref="IMigrationReporter"/> that presents user-facing output.
    /// </summary>
    /// <param name="diffRenderer">Renders the migration diff as human-readable text.</param>
    /// <param name="sqlPlanRenderer">Renders the SQL plan as human-readable text.</param>
    /// <param name="output">The writer for informational output (typically stdout).</param>
    /// <param name="error">The writer for errors and warnings (typically stderr).</param>
    public DefaultMigrationReporter(IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer, TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
        _diffRenderer = diffRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
    }

    public string Format => MigrationRunOptions.DefaultOutputFormat;

    public void Info(string message) => _output.WriteLine(message);

    public void Error(string message) => _error.WriteLine(message);

    public void ReportDiff(MigrationDiff diff)
    {
        var render = _diffRenderer.Render(diff);
        _output.WriteLine(render);
        _output.WriteLine();
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        var render = _sqlPlanRenderer.Render(plan);
        _output.WriteLine(render);
        _output.WriteLine();
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        _output.WriteLine("Policy diagnostics:");
        if (diagnostics.Count == 0)
        {
            _output.WriteLine("- Nothing to report");
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var writer = diagnostic.Severity is PolicyDiagnosticSeverity.Error or PolicyDiagnosticSeverity.Warning ? _error : _output;
            writer.WriteLine($"- {diagnostic.PolicyName}: {diagnostic.Message}");
        }
    }
}
