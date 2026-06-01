using Microsoft.Extensions.Logging;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationReporter"/> that presents user-facing output via <see cref="ILogger"/>.
/// The terminal logger provider (registered by default) routes log output to stdout/stderr; any structured
/// sink the consumer adds also receives the same events.
/// </summary>
/// <param name="logger">The logger to write output to.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
internal sealed class DefaultMigrationReporter(ILogger<DefaultMigrationReporter> logger, IMigrationPlanRenderer planRenderer) : IMigrationReporter
{
    public void Info(string message) => logger.Log(LogLevel.Information, message);

    public void Error(string message) => logger.Log(LogLevel.Error, message);

    public void ReportPlan(MigrationPlan plan)
    {
        logger.Log(LogLevel.Information, planRenderer.Render(plan));

        // A blank line separates the diff from following output. This is terminal layout only, so it
        // doesn't go through the logger.
        Console.Out.WriteLine();
    }

    public void ReportPreview(IReadOnlyList<string> statements)
    {
        foreach (var statement in statements)
        {
            logger.Log(LogLevel.Information, statement);
        }
    }

    public void ReportDiagnostics(IReadOnlyList<PolicyError> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var level = diagnostic.Severity switch
            {
                PolicySeverity.Error => LogLevel.Error,
                PolicySeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };
            logger.Log(level, "{DiagnosticPolicyName}: {DiagnosticMessage}", diagnostic.PolicyName, diagnostic.Message);
        }
    }
}
