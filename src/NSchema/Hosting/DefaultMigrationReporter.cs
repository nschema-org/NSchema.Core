using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationReporter"/> that presents user-facing output.
/// </summary>
/// <param name="output">The writer for informational output (typically stdout).</param>
/// <param name="error">The writer for errors and warnings (typically stderr).</param>
/// <param name="diffRenderer">Renders the migration diff as human-readable text.</param>
internal sealed class DefaultMigrationReporter(TextWriter output, TextWriter error, IDiffRenderer diffRenderer) : IMigrationReporter
{
    public void Info(string message) => output.WriteLine(message);

    public void Error(string message) => error.WriteLine(message);

    public void ReportDiff(MigrationDiff diff)
    {
        var renderer = diffRenderer.Render(diff);
        output.WriteLine(renderer);
        output.WriteLine();
    }

    public void ReportPreview(IReadOnlyList<string> statements)
    {
        output.WriteLine("SQL Preview:");
        if (statements.Count == 0)
        {
            output.WriteLine("- No statements to execute");
        }
        else
        {
            foreach (var statement in statements)
            {
                output.WriteLine(statement);
            }
        }
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
