using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>GRANT privilege[, privilege]… ON schema.table TO role;</c>
/// </summary>
/// <param name="Privileges">The granted privileges as written.</param>
/// <param name="On">The table granted on.</param>
/// <param name="Role">The role granted to.</param>
public sealed record GrantTableStatement(SeparatedSyntaxList<Privilege> Privileges, QualifiedName On, Identifier Role) : NsqlStatement
{
    /// <summary>The granted privileges folded into a flag set.</summary>
    public TablePrivilege PrivilegeFlags
    {
        get
        {
            var flags = TablePrivilege.None;
            foreach (var privilege in Privileges)
            {
                flags |= privilege.Value;
            }
            return flags;
        }
    }

    /// <summary>The <c>GRANT</c> keyword token, when parsed.</summary>
    public Token? GrantKeyword { get; init; }

    /// <summary>The <c>ON</c> keyword token, when parsed.</summary>
    public Token? OnKeyword { get; init; }

    /// <summary>The <c>TO</c> keyword token, when parsed.</summary>
    public Token? ToKeyword { get; init; }

    /// <summary>The terminating <c>;</c> token, when parsed.</summary>
    public Token? SemicolonToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (GrantKeyword is { } grant)
            {
                yield return grant;
            }
            foreach (var child in Privileges.Children)
            {
                yield return child;
            }
            if (OnKeyword is { } on)
            {
                yield return on;
            }
            yield return On;
            if (ToKeyword is { } to)
            {
                yield return to;
            }
            yield return Role;
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
