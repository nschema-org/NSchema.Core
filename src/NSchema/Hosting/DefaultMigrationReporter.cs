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
/// <param name="output">The writer for informational output (typically stdout).</param>
/// <param name="error">The writer for errors and warnings (typically stderr).</param>
/// <param name="diffRenderer">Renders the migration diff as human-readable text.</param>
/// <param name="sqlPlanRenderer">Renders the SQL plan as human-readable text.</param>
internal sealed class DefaultMigrationReporter(TextWriter output, TextWriter error, IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer) : IMigrationReporter
{
    public void Info(string message) => output.WriteLine(message);

    public void Error(string message) => error.WriteLine(message);

    public void ReportDiff(MigrationDiff diff)
    {
        var render = diffRenderer.Render(diff);
        output.WriteLine(render);
        output.WriteLine();
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        var render = sqlPlanRenderer.Render(plan);
        output.WriteLine(render);
        output.WriteLine();
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        output.WriteLine("Policy diagnostics:");
        if (diagnostics.Count == 0)
        {
            output.WriteLine("- Nothing to report");
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var writer = diagnostic.Severity is PolicyDiagnosticSeverity.Error or PolicyDiagnosticSeverity.Warning ? error : output;
            writer.WriteLine($"- {diagnostic.PolicyName}: {diagnostic.Message}");
        }
    }
}
