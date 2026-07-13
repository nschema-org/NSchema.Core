
namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A node in the NSchema language syntax tree: what was written, where it was written.
/// </summary>
public abstract record NsqlNode
{
    /// <summary>
    /// The position in the source where the node begins.
    /// </summary>
    public required SourcePosition Position { get; init; }
}
