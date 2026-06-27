using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Sql.Model;

namespace NSchema.Plan.PlanFile;

/// <summary>
/// The envelope for persisting a saved plan file.
/// </summary>
/// <param name="Plan">The structured migration plan that was reviewed.</param>
/// <param name="Sql">The SQL plan generated from <see cref="Plan"/>.</param>
/// <param name="Diff">The structured diff the plan was derived from.</param>
/// <param name="CreatedAt">When the plan was created.</param>
public sealed record PlanFileEnvelope(
    MigrationPlan Plan,
    SqlPlan Sql,
    DatabaseDiff Diff,
    DateTimeOffset CreatedAt
)
{
    /// <summary>
    /// The current envelope format version.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The format version of the payload. Owned by the format, not the caller: it defaults to
    /// <see cref="CurrentVersion"/> on write and is validated by the writer on read.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;
}
