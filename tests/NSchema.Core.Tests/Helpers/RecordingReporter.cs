using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Sql.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Captures the structured artifacts the pipeline hands to the reporter, so end-to-end tests can assert on
/// orchestration without coupling to rendered text (which the renderer tests already cover).
/// </summary>
/// <remarks>
/// Uses a distinct <see cref="Format"/> so it coexists with the built-in human reporter without colliding;
/// end-to-end tests select it via <c>new NSchemaApplicationOptions { Reporter = RecordingReporter.FormatName }</c>.
/// </remarks>
internal sealed class RecordingReporter : IOperationReporter
{
    public const string FormatName = "recording";

    public List<string> Infos { get; } = [];
    public List<Exception> Exceptions { get; } = [];
    public DatabaseDiff? Diff { get; private set; }
    public MigrationPlan? Plan { get; private set; }
    public SqlPlan? SqlPlan { get; private set; }
    public List<PolicyDiagnostic> Diagnostics { get; } = [];

    public void Info(string message) => Infos.Add(message);
    public void ReportException(Exception exception) => Exceptions.Add(exception);
    public void ReportDiff(DatabaseDiff diff) => Diff = diff;
    public void ReportPlan(MigrationPlan plan) => Plan = plan;
    public void ReportSqlPlan(SqlPlan plan) => SqlPlan = plan;
    public void ReportDiagnostics(PolicyDiagnostics diagnostics) => Diagnostics.AddRange(diagnostics);
}
