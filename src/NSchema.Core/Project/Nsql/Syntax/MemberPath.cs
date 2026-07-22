using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A change-event target path as written: <c>schema.table.member</c> at the top level, or the
/// unqualified <c>table.member</c> inside a template body (the schema binds at projection).
/// </summary>
/// <param name="Schema">The schema segment, or <see langword="null"/> when written unqualified.</param>
/// <param name="Table">The table segment.</param>
/// <param name="Member">The member (column or constraint) segment.</param>
public sealed record MemberPath(Identifier? Schema, Identifier Table, Identifier Member) : NsqlNode
{
    /// <summary>
    /// The <c>.</c> token after the schema segment, when parsed qualified.
    /// </summary>
    public Token? SchemaDotToken { get; init; }

    /// <summary>
    /// The <c>.</c> token before the member segment, when parsed.
    /// </summary>
    public Token? MemberDotToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (Schema != null)
            {
                yield return Schema;
                if (SchemaDotToken is { } schemaDot)
                {
                    yield return schemaDot;
                }
            }
            yield return Table;
            if (MemberDotToken is { } memberDot)
            {
                yield return memberDot;
            }
            yield return Member;
        }
    }
}
