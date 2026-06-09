using NSchema.Plan.Model;
using NSchema.Sql.Model;

namespace NSchema.Plan.PlanFile;

/// <summary>
/// The envelope for persisting a saved plan file.
/// </summary>
/// <param name="Version">The version of the envelope format.</param>
/// <param name="Plan">The structured migration plan that was reviewed.</param>
/// <param name="Sql">The SQL plan generated from <see cref="Plan"/>.</param>
/// <param name="CreatedAt">When the plan file was written.</param>
internal sealed record PlanFileEnvelope(
    int Version,
    MigrationPlan Plan,
    SqlPlan Sql,
    DateTimeOffset CreatedAt
)
{
    /// <summary>
    /// The current envelope format version.
    /// </summary>
    public const int CurrentVersion = 1;
}
