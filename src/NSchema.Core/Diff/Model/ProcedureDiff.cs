using NSchema.Schema.Model.Procedures;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a procedure.
/// </summary>
/// <param name="Schema">The name of the schema the procedure belongs to.</param>
/// <param name="Name">The procedure name.</param>
/// <param name="Kind">The change to the procedure.</param>
/// <param name="RenamedFrom">The previous procedure name when the procedure is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The desired procedure for an add, or for any modification that replaces or recreates it; otherwise <see langword="null"/>.</param>
/// <param name="Arguments">The change to the argument list, set when the signature changed (which forces a recreate).</param>
/// <param name="Comment">The change to the procedure's comment, if any.</param>
public sealed record ProcedureDiff(
    string Schema,
    string Name,
    ChangeKind Kind,
    string? RenamedFrom = null,
    Procedure? Definition = null,
    ValueChange<string>? Arguments = null,
    ValueChange<string>? Comment = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// The signature changed, so the procedure must be dropped and recreated: replacing in place would leave
    /// the old signature behind as a separate overload in the database.
    /// </summary>
    public bool RequiresRecreate => Arguments is not null;
}
