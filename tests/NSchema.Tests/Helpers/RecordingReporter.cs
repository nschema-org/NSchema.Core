using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Sql.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Captures the structured artifacts the pipeline hands to the reporter, so end-to-end tests can assert on
/// orchestration without coupling to rendered text (which the renderer tests already cover).
/// </summary>
/// <remarks>
/// Uses a distinct <see cref="Format"/> so it coexists with the built-in human reporter without colliding;
/// end-to-end tests select it via <c>WithOutputFormat(RecordingReporter.FormatName)</c>.
/// </remarks>
internal sealed class RecordingReporter : IMigrationReporter
{
    public const string FormatName = "recording";

    public string Format => FormatName;

    public List<string> Infos { get; } = [];
    public List<string> Errors { get; } = [];
    public MigrationDiff? Diff { get; private set; }
    public SqlPlan? SqlPlan { get; private set; }
    public List<PolicyDiagnostic> Diagnostics { get; } = [];

    public void Info(string message) => Infos.Add(message);
    public void Error(string message) => Errors.Add(message);
    public void ReportDiff(MigrationDiff diff) => Diff = diff;
    public void ReportSqlPlan(SqlPlan plan) => SqlPlan = plan;
    public void ReportDiagnostics(PolicyDiagnostics diagnostics) => Diagnostics.AddRange(diagnostics);
}
