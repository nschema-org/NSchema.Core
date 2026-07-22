using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Blocks;

namespace NSchema.Project.Nsql;

/// <summary>
/// A parsed block document — the <c>KEYWORD [label] ( … );</c> statements as written, in order. Both the
/// configuration file and the lockfile parse to this shape; what keywords are legal is the reader's rule.
/// </summary>
/// <param name="Statements">The block statements in source order.</param>
public sealed record NsqlBlockDocument(IReadOnlyList<BlockStatement> Statements) : NsqlSourceDocument
{
    private protected override IReadOnlyList<NsqlStatement> StatementNodes => Statements;
}
