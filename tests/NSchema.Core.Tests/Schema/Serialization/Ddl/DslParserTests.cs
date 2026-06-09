using NSchema.Schema.Model;
using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DslParserTests
{
    private static DatabaseSchema Parse(string source) => new DslParser(source).Parse();

    private static SchemaDefinition ParseSingleSchema(string source) => Parse(source).Schemas.ShouldHaveSingleItem();

    // -------------------------------------------------------------------------
    // Schemas
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Empty_ProducesEmptySchema()
    {
        var schema = Parse("");
        schema.Schemas.ShouldBeEmpty();
        schema.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_CreateSchema_ProducesSchema()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app;");
        schema.Name.ShouldBe("app");
        schema.IsPartial.ShouldBeFalse();
        schema.OldName.ShouldBeNull();
        schema.Comment.ShouldBeNull();
    }

    [Fact]
    public void Parse_CreatePartialSchema_SetsIsPartial()
        => ParseSingleSchema("CREATE PARTIAL SCHEMA app;").IsPartial.ShouldBeTrue();

    [Fact]
    public void Parse_CreateSchemaRenamedFrom_SetsOldName()
        => ParseSingleSchema("CREATE SCHEMA app RENAMED FROM legacy_app;").OldName.ShouldBe("legacy_app");

    [Fact]
    public void Parse_DocCommentBeforeSchema_BecomesComment()
        => ParseSingleSchema("--- The application schema.\nCREATE SCHEMA app;").Comment.ShouldBe("The application schema.");

    [Fact]
    public void Parse_DocBlockBeforeSchema_BecomesComment()
        => ParseSingleSchema("/** Block doc. */ CREATE SCHEMA app;").Comment.ShouldBe("Block doc.");

    [Fact]
    public void Parse_SourceCommentBeforeSchema_IsNotACatalogComment()
        => ParseSingleSchema("-- just a note\nCREATE SCHEMA app;").Comment.ShouldBeNull();

    [Fact]
    public void Parse_KeywordsAreCaseInsensitive()
        => ParseSingleSchema("create partial schema app renamed from old;").IsPartial.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DropSchema_RecordsDroppedSchema()
        => Parse("DROP SCHEMA scratch;").DroppedSchemas.ShouldHaveSingleItem().ShouldBe("scratch");

    [Fact]
    public void Parse_DropTable_VivifiesSchemaAndRecordsDroppedTable()
    {
        var schema = ParseSingleSchema("DROP TABLE app.old_table;");
        schema.Name.ShouldBe("app");
        schema.DroppedTables.ShouldHaveSingleItem().ShouldBe("old_table");
    }

    [Fact]
    public void Parse_DropTableInDeclaredSchema_MergesOntoSameSchema()
    {
        var schema = ParseSingleSchema("CREATE PARTIAL SCHEMA app; DROP TABLE app.old;");
        schema.IsPartial.ShouldBeTrue();
        schema.DroppedTables.ShouldHaveSingleItem().ShouldBe("old");
    }

    // -------------------------------------------------------------------------
    // Configuration blocks (reserved — skipped)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ConfigBlock_IsSkipped()
    {
        var schema = Parse(
            """
            nschema {
              dialect = 'postgres'
            }
            CREATE SCHEMA app;
            """);
        schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Parse_LabelledConfigBlock_IsSkipped()
    {
        var schema = Parse(
            """
            backend 'file' {
              path = 'state/app.nsstate'
            }
            CREATE SCHEMA app;
            """);
        schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Parse_NestedConfigBlock_IsSkippedWholesale()
    {
        var schema = Parse(
            """
            provider 'postgres' {
              pool {
                max = 10
              }
            }
            CREATE SCHEMA app;
            """);
        schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Parse_ConfigBlockWithBraceInString_BalancesCorrectly()
    {
        // A '}' inside a string is a String token, not a closing brace, so balancing must not be fooled by it.
        var schema = Parse("provider 'p' { note = '}' } CREATE SCHEMA app;");
        schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Parse_UnterminatedConfigBlock_Throws()
        => Should.Throw<DslSyntaxException>(() => Parse("backend 'file' { path = 'x'"))
            .Message.ShouldContain("Unterminated configuration block");

    // -------------------------------------------------------------------------
    // Multiple statements
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_MultipleStatements_AccumulateInOrder()
    {
        var schema = Parse(
            """
            CREATE SCHEMA app;
            CREATE SCHEMA reporting;
            DROP SCHEMA scratch;
            """);
        schema.Schemas.Select(s => s.Name).ShouldBe(["app", "reporting"]);
        schema.DroppedSchemas.ShouldHaveSingleItem().ShouldBe("scratch");
    }

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_MissingSemicolon_Throws()
        => Should.Throw<DslSyntaxException>(() => Parse("CREATE SCHEMA app")).Message.ShouldContain("';'");

    [Fact]
    public void Parse_DuplicateSchema_Throws()
        => Should.Throw<DslSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE SCHEMA app;"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_UnknownAfterCreate_Throws()
        => Should.Throw<DslSyntaxException>(() => Parse("CREATE THING app;")).Message.ShouldContain("Expected SCHEMA or TABLE");

    [Fact]
    public void Parse_PartialTable_Throws()
        => Should.Throw<DslSyntaxException>(() => Parse("CREATE PARTIAL TABLE app.t ();")).Message.ShouldContain("PARTIAL applies to SCHEMA");

    [Fact]
    public void Parse_CreateTable_NotYetSupported()
        => Should.Throw<DslSyntaxException>(() => Parse("CREATE TABLE app.users ();")).Message.ShouldContain("not yet supported");
}
