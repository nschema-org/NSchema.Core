using NSchema.Project.Domain.Models;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for the database-global <c>CREATE EXTENSION</c> / <c>DROP EXTENSION</c> statements. Extensions
/// live at the root of the parsed <see cref="Database"/>, not inside a schema.
/// </summary>
public sealed class NsqlParserExtensionTests
{
    private static Database Parse(string source) => new TestNsqlParser(source).Parse().Database;

    [Fact]
    public void Parse_CreateExtension_Bare_RecordsRootLevelExtension()
    {
        var extension = Parse("CREATE EXTENSION citext;").Extensions.ShouldHaveSingleItem();
        ShouldlyIdentifierExtensions.ShouldBe(extension.Name, "citext");
        extension.Version.ShouldBeNull();
    }

    [Fact]
    public void Parse_CreateExtension_WithVersion_CapturesVersion()
    {
        var extension = Parse("CREATE EXTENSION postgis VERSION '3.4';").Extensions.ShouldHaveSingleItem();
        ShouldlyIdentifierExtensions.ShouldBe(extension.Name, "postgis");
        extension.Version.ShouldBe("3.4");
    }

    [Fact]
    public void Parse_CreateExtension_QuotedName_AllowsNonIdentifierCharacters()
        // Extension names commonly contain a hyphen (e.g. uuid-ossp), which a bare identifier cannot express.
        => ShouldlyIdentifierExtensions.ShouldBe(Parse("CREATE EXTENSION 'uuid-ossp';").Extensions.ShouldHaveSingleItem().Name, "uuid-ossp");

    [Fact]
    public void Parse_CreateExtension_WithDocComment_AttachesComment()
        => Parse("--- spatial types\nCREATE EXTENSION postgis;")
            .Extensions.ShouldHaveSingleItem().Comment.ShouldBe("spatial types");

    [Fact]
    public void Parse_CreateExtension_IsNotScopedToASchema()
    {
        // An extension declared alongside a schema still lands at the root, not inside the schema.
        var schema = Parse("CREATE SCHEMA app; CREATE EXTENSION citext;");
        ShouldlyIdentifierExtensions.ShouldBe(schema.Extensions.ShouldHaveSingleItem().Name, "citext");
        ShouldlyIdentifierExtensions.ShouldBe(schema.Schemas.ShouldHaveSingleItem().Name, "app");
    }

    [Fact]
    public void Parse_DropExtension_BecomesADirective()
        => ShouldlyIdentifierExtensions.ShouldBe(Directives("DROP EXTENSION citext;")
            .Extensions.Drops.ShouldHaveSingleItem(), "citext");

    [Fact]
    public void Parse_DuplicateExtension_FailsTheRead()
        => new TestNsqlParser("CREATE EXTENSION citext; CREATE EXTENSION citext;")
            .Project().Errors.ShouldHaveSingleItem().Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialExtension_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Parse("CREATE PARTIAL EXTENSION citext;"))
            .Message.ShouldContain("after CREATE");

    [Fact]
    public void Parse_CreateExtension_MissingVersionString_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Parse("CREATE EXTENSION postgis VERSION;"))
            .Message.ShouldContain("a version string");

    private static NSchema.Project.Domain.Models.ProjectDirectives Directives(string source)
    {
        var read = NSchema.Project.Nsql.NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        return NSchema.Project.ProjectAssembler.Assemble([read.Value]).Value!.Directives;
    }
}
