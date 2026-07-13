using NSchema.Project.Ddl.Models;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Helpers;

/// <summary>
/// The parse-and-project pipeline with the old parser's shape: construct with source, <see cref="Parse"/> to a
/// <see cref="DdlDocument"/>, syntax and assembly errors thrown as <c>DdlSyntaxException</c> (the reader folds
/// them into a <c>Result</c>; these tests assert the raw errors).
/// </summary>
internal sealed class TestDdlParser(string source)
{
    public DdlDocument Parse()
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
