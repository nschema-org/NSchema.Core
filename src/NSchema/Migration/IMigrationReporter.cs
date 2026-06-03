using NSchema.Diff.Model;
using NSchema.Policies;
using NSchema.Sql.Model;

namespace NSchema.Migration;

/// <summary>
/// Presents user-facing progress and outcomes for a migration run.
/// </summary>
public interface IMigrationReporter
{
    /// <summary>
    /// Reports a status / progress message to the user.
    /// </summary>
    void Info(string message);

    /// <summary>
    /// Reports an error to the user.
    /// </summary>
    void Error(string message);

    /// <summary>
    /// Presents the computed migration diff as human-readable output.
    /// </summary>
    void ReportDiff(MigrationDiff diff);

    /// <summary>
    /// Presents the SQL plan a migration would run.
    /// </summary>
    void ReportSqlPlan(SqlPlan plan);

    /// <summary>
    /// Presents non-fatal policy diagnostics (warnings and info) produced during planning.
    /// </summary>
    void ReportDiagnostics(PolicyDiagnostics diagnostics);
}
