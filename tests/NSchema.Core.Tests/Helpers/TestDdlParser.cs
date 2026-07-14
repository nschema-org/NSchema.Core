using NSchema.Project.Nsql;

namespace NSchema.Tests.Helpers;

/// <summary>
/// The parse-and-project pipeline with the old parser's shape: construct with source, <see cref="Parse"/> to a
/// projected document, syntax and assembly errors thrown as <c>DdlSyntaxException</c> (the reader folds
/// them into a <c>Result</c>; these tests assert the raw errors).
/// </summary>
internal sealed class TestDdlParser(string source)
{
    public ProjectedDocument Parse() => Project().Require();

    /// <summary>
    /// The full projection result: parse errors still throw (the grammar tests assert the raw exceptions),
    /// while assembly findings — duplicates, unknown references, broken template bodies — ride as diagnostics.
    /// </summary>
    public Result<ProjectedDocument, NsqlDiagnostic> Project()
    {
        var parser = new NsqlParser(source);
        var document = parser.Parse();
        if (parser.Errors.Count > 0)
        {
            throw parser.Errors[0];
        }
        return DocumentProjector.Project(document);
    }
}
