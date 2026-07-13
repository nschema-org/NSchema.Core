using NSchema.Project.Ddl.Models;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A node in the NSchema language syntax tree: what was written, where it was written.
/// </summary>
/// <remarks>
/// The syntax tree is the language lane's own model — it references no schema-domain types beyond the
/// vocabulary primitives (<c>SqlText</c> for opaque SQL). Projection translates nodes into the domain.
/// (<see cref="SourcePosition"/> moves into this lane when the reader is realigned.)
/// </remarks>
public abstract record NsqlNode
{
    /// <summary>
    /// The position in the source where the node begins.
    /// </summary>
    public required SourcePosition Position { get; init; }
}
