using NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// A parsed NSchema project source file: the statements as written, in order. One document is one file.
/// </summary>
/// <param name="Statements">The top-level statements in source order.</param>
public sealed record NsqlDocument(IReadOnlyList<NsqlStatement> Statements) : NsqlSourceDocument
{
    private protected override IReadOnlyList<NsqlStatement> StatementNodes => Statements;
}
