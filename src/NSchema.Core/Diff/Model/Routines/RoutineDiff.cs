using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Diff.Model.Routines;

/// <summary>
/// Describes a change to a routine (a function or a procedure).
/// </summary>
/// <param name="Schema">The name of the schema the routine belongs to.</param>
/// <param name="Name">The routine name.</param>
/// <param name="Kind">The change to the routine.</param>
/// <param name="RoutineKind">Whether the routine is a function or a procedure (carried so the correct statement is emitted for a rename, comment change, or removal).</param>
/// <param name="RenamedFrom">The previous routine name when the routine is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The desired routine for an add, or for any modification that replaces or recreates it; otherwise <see langword="null"/>.</param>
/// <param name="Arguments">The change to the argument list, set when the signature changed (which forces a recreate).</param>
/// <param name="Comment">The change to the routine's comment, if any.</param>
public sealed record RoutineDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    RoutineKind RoutineKind,
    SqlIdentifier? RenamedFrom = null,
    Routine? Definition = null,
    ValueChange<SqlText>? Arguments = null,
    ValueChange<string>? Comment = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// The signature changed, so the routine must be dropped and recreated: replacing in place would leave the
    /// old signature behind as a separate overload in the database.
    /// </summary>
    public bool RequiresRecreate => Arguments is not null;
}
