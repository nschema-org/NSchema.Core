using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a function.
/// </summary>
/// <param name="Schema">The name of the schema the function belongs to.</param>
/// <param name="Name">The function name.</param>
/// <param name="Kind">The change to the function.</param>
/// <param name="RenamedFrom">The previous function name when the function is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The desired function for an add, or for any modification that replaces or recreates it; otherwise <see langword="null"/>.</param>
/// <param name="Arguments">The change to the argument list, set when the signature changed (which forces a recreate).</param>
/// <param name="Comment">The change to the function's comment, if any.</param>
public sealed record FunctionDiff(
    string Schema,
    string Name,
    ChangeKind Kind,
    string? RenamedFrom = null,
    Function? Definition = null,
    ValueChange<string>? Arguments = null,
    ValueChange<string>? Comment = null
)
{
    /// <summary>
    /// The signature changed, so the function must be dropped and recreated: replacing in place would leave the
    /// old signature behind as a separate overload in the database.
    /// </summary>
    public bool RequiresRecreate => Arguments is not null;
}
