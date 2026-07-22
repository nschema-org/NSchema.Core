using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>RENAME COLUMN schema.table.column TO name;</c>
/// </summary>
/// <param name="From">The column's current address.</param>
/// <param name="To">The name the column is renamed to.</param>
public sealed record RenameColumnStatement(MemberPath From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token, when parsed.
    /// </summary>
    public Token? RenameKeyword { get; init; }

    /// <summary>
    /// The <c>COLUMN</c> keyword token, when parsed.
    /// </summary>
    public Token? ColumnKeyword { get; init; }

    /// <summary>
    /// The <c>TO</c> keyword token, when parsed.
    /// </summary>
    public Token? ToKeyword { get; init; }

    /// <summary>
    /// The terminating <c>;</c> token, when parsed.
    /// </summary>
    public Token? SemicolonToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (RenameKeyword is { } rename)
            {
                yield return rename;
            }
            if (ColumnKeyword is { } column)
            {
                yield return column;
            }
            yield return From;
            if (ToKeyword is { } to)
            {
                yield return to;
            }
            yield return To;
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
