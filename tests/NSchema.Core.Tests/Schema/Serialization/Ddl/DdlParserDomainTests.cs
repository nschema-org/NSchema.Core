using NSchema.Project.Nsql;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Domains;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Parser coverage for <c>CREATE DOMAIN s.d AS &lt;type&gt; [NOT NULL] [CONSTRAINT n CHECK (e)]… [DEFAULT expr]</c>.
/// </summary>
public sealed class DdlParserDomainTests
{
    private static DomainDefinition ParseDomain(string sql) =>
        new TestDdlParser("CREATE SCHEMA app; " + sql).Parse().Schema
            .Schemas.ShouldHaveSingleItem().Domains.ShouldHaveSingleItem();

    [Fact]
    public void Parse_SimpleDomain_CapturesNameAndType()
    {
        var domain = ParseDomain("CREATE DOMAIN app.typeid AS text;");
        ShouldlyIdentifierExtensions.ShouldBe(domain.Name, "typeid");
        domain.DataType.ShouldBe(SqlType.Text);
        domain.NotNull.ShouldBeFalse();
        domain.Default.ShouldBeNull();
        domain.Checks.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NotNull_SetsFlag()
        => ParseDomain("CREATE DOMAIN app.d AS text NOT NULL;").NotNull.ShouldBeTrue();

    [Fact]
    public void Parse_Default_IsCapturedVerbatim()
        => ShouldlyIdentifierExtensions.ShouldBe(ParseDomain("CREATE DOMAIN app.d AS text DEFAULT 'n/a';").Default, "'n/a'");

    [Fact]
    public void Parse_Check_IsCaptured()
    {
        var check = ParseDomain("CREATE DOMAIN app.d AS text CONSTRAINT d_chk CHECK (VALUE <> '');").Checks.ShouldHaveSingleItem();
        ShouldlyIdentifierExtensions.ShouldBe(check.Name, "d_chk");
        ShouldlyIdentifierExtensions.ShouldBe(check.Expression, "VALUE <> ''");
    }

    [Fact]
    public void Parse_AllClauses_CapturesEachInOrder()
    {
        var domain = ParseDomain(
            "CREATE DOMAIN app.email AS text NOT NULL CONSTRAINT email_fmt CHECK (VALUE ~ '@') DEFAULT 'x@y';");
        domain.DataType.ShouldBe(SqlType.Text);
        domain.NotNull.ShouldBeTrue();
        ShouldlyIdentifierExtensions.ShouldBe(domain.Checks.ShouldHaveSingleItem().Name, "email_fmt");
        ShouldlyIdentifierExtensions.ShouldBe(domain.Default, "'x@y'");
    }

    [Fact]
    public void Parse_RenamedFrom_SetsOldName()
        => ShouldlyIdentifierExtensions.ShouldBe(ParseDomain("CREATE DOMAIN app.typeid RENAMED FROM legacy_id AS text;").OldName, "legacy_id");

    [Fact]
    public void Parse_WithDocComment_AttachesComment()
        => ParseDomain("--- unique id\nCREATE DOMAIN app.typeid AS text;").Comment.ShouldBe("unique id");

    [Fact]
    public void Parse_DropDomain_RecordsDroppedDomain()
        => ShouldlyIdentifierExtensions.ShouldBe(new TestDdlParser("CREATE SCHEMA app; DROP DOMAIN app.typeid;").Parse().Schema
                .Schemas.ShouldHaveSingleItem().DroppedDomains.ShouldHaveSingleItem(), "typeid");

    [Fact]
    public void Parse_DuplicateDomain_FailsTheRead()
        => new TestDdlParser("CREATE SCHEMA app; CREATE DOMAIN app.d AS text; CREATE DOMAIN app.d AS int;").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialDomain_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestDdlParser("CREATE PARTIAL DOMAIN app.d AS text;").Parse())
            .Message.ShouldContain("PARTIAL applies to SCHEMA");

    [Fact]
    public void Parse_UnknownClause_Throws()
        => Should.Throw<NsqlSyntaxException>(() =>
            new TestDdlParser("CREATE SCHEMA app; CREATE DOMAIN app.d AS text WIBBLE;").Parse())
            .Message.ShouldContain("Expected NOT NULL, NULL, CONSTRAINT");
}
