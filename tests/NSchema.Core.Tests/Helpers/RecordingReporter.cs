using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Captures the structured artifacts the pipeline hands to the reporter, so end-to-end tests can assert on
/// orchestration without coupling to rendered text (which the renderer tests already cover).
/// </summary>
/// <remarks>
/// End-to-end tests register it via <c>builder.UseReporter(reporter)</c>, replacing the built-in human reporter.
/// </remarks>
internal sealed class RecordingReporter : IOperationReporter
{
    /// <summary>Every reported message, in order, paired with its <see cref="MessageKind"/>.</summary>
    public List<(MessageKind Kind, string Message)> Messages { get; } = [];

    /// <summary>The text of every reported message, regardless of kind.</summary>
    public List<string> Infos { get; } = [];
    public List<Exception> Exceptions { get; } = [];
    public DatabaseSchema? Schema { get; private set; }
    public DatabaseDiff? Diff { get; private set; }
    public MigrationPlan? Plan { get; private set; }
    public SqlPlan? SqlPlan { get; private set; }
    public List<PolicyDiagnostic> Diagnostics { get; } = [];

    public void Report(MessageKind kind, string message)
    {
        Messages.Add((kind, message));
        Infos.Add(message);
    }
    public void ReportException(Exception exception) => Exceptions.Add(exception);
    public void ReportSchema(DatabaseSchema schema) => Schema = schema;
    public void ReportDiff(DatabaseDiff diff) => Diff = diff;
    public void ReportPlan(MigrationPlan plan) => Plan = plan;
    public void ReportSqlPlan(SqlPlan plan) => SqlPlan = plan;
    public void ReportDiagnostics(PolicyDiagnostics diagnostics) => Diagnostics.AddRange(diagnostics);
}
