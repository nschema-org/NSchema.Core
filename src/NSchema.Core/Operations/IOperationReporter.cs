using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Sql.Model;

namespace NSchema.Operations;

/// <summary>
/// Presents user-facing progress and outcomes for a migration run.
/// </summary>
public interface IOperationReporter
{
    /// <summary>
    /// Reports a status / progress message to the user.
    /// </summary>
    void Info(string message);

    /// <summary>
    /// Reports an error to the user. Receives the original <see cref="Exception"/> so the reporter can present it however suits its output format.
    /// </summary>
    /// <param name="exception">The exception that caused the operation to fail.</param>
    void ReportException(Exception exception);

    /// <summary>
    /// Presents the computed migration diff as human-readable output.
    /// </summary>
    void ReportDiff(DatabaseDiff diff);

    /// <summary>
    /// Presents plan-level detail that isn't part of the diff, such as the pre- and post-deployment scripts.
    /// </summary>
    void ReportPlan(MigrationPlan plan);

    /// <summary>
    /// Presents the SQL plan a migration would run.
    /// </summary>
    void ReportSqlPlan(SqlPlan plan);

    /// <summary>
    /// Presents non-fatal policy diagnostics (warnings and info) produced during planning.
    /// </summary>
    void ReportDiagnostics(PolicyDiagnostics diagnostics);
}
