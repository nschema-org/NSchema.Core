using System.Collections;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A parenthesised list of column names, <c>(a, b, …)</c> — the parentheses plus the separated identifiers.
/// Behaves as the list of columns for consumers, and reprints exactly for the printer.
/// </summary>
/// <param name="Columns">The column identifiers with their separators.</param>
public sealed record ColumnList(SeparatedSyntaxList<Identifier> Columns) : NsqlNode, IReadOnlyList<Identifier>
{
    /// <summary>
    /// The <c>(</c> token, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token, when parsed.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    /// <inheritdoc/>
    public int Count => Columns.Count;

    /// <inheritdoc/>
    public Identifier this[int index] => Columns[index];

    /// <inheritdoc/>
    public IEnumerator<Identifier> GetEnumerator() => Columns.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            foreach (var child in Columns.Children)
            {
                yield return child;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
            }
        }
    }

    /// <summary>
    /// Builds a synthetic column list (no source) from <paramref name="columns"/>.
    /// </summary>
    public static ColumnList Synthetic(IReadOnlyList<Identifier> columns) =>
        new(new SeparatedSyntaxList<Identifier>(columns)) { Position = SourcePosition.None };
}
