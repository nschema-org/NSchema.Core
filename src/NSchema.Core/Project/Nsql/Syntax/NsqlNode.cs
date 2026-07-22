using System.Text;

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

    /// <summary>
    /// The node's tokens and child nodes in source order.
    /// A token-bearing (parsed) node yields all of them so the printer round-trips;
    /// a node built without tokens (or not yet lowered) yields none.
    /// </summary>
    internal virtual IEnumerable<NsqlChild> Children => [];

    /// <summary>
    /// Reprints the node as source text.
    /// </summary>
    public string ToSource()
    {
        var sb = new StringBuilder();
        WriteTo(sb);
        return sb.ToString();
    }

    internal void WriteTo(StringBuilder sb)
    {
        foreach (var child in Children)
        {
            child.WriteTo(sb);
        }
    }
}
