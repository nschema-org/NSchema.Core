using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationReporter"/> that presents user-facing output.
/// </summary>
/// <param name="output">The writer for informational output (typically stdout).</param>
/// <param name="error">The writer for errors and warnings (typically stderr).</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
internal sealed class DefaultMigrationReporter(TextWriter output, TextWriter error, IMigrationPlanRenderer planRenderer) : IMigrationReporter
{
    public void Info(string message) => output.WriteLine(message);

    public void Error(string message) => error.WriteLine(message);

    public void ReportPlan(MigrationPlan plan)
    {
        output.WriteLine(planRenderer.Render(plan));
        output.WriteLine();
    }

    public void ReportPreview(IReadOnlyList<string> statements)
    {
        output.WriteLine("SQL Preview:");
        foreach (var statement in statements)
        {
            output.WriteLine(statement);
        }
    }

    public void ReportDiagnostics(IReadOnlyList<PolicyError> diagnostics)
    {
        output.WriteLine("Policy diagnostics:");
        if (diagnostics.Count == 0)
        {
            output.WriteLine("None");
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var writer = diagnostic.Severity is PolicySeverity.Error or PolicySeverity.Warning ? error : output;
            writer.WriteLine($"- {diagnostic.PolicyName}: {diagnostic.Message}");
        }
    }
}
