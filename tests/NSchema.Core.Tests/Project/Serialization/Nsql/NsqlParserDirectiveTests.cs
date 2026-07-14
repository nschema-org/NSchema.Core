using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Covers the directive statements — <c>RENAME &lt;kind&gt; &lt;current&gt; TO &lt;name&gt;;</c> and
/// <c>PARTIAL SCHEMA name;</c>. Statements declare state; directives steer management, so they assemble
/// onto <see cref="ProjectDirectives"/> rather than the schema tree.
/// </summary>
public sealed class NsqlParserDirectiveTests
{
    /// <summary>Assembles the source and returns its directives (declarations included so validation passes).</summary>
    private static ProjectDirectives Directives(string source)
    {
        var read = NSchema.Project.Nsql.NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        return NSchema.Project.ProjectAssembler.Assemble([read.Value]).Value!.Directives;
    }

    private static ObjectReference App(string name) => new(new SqlIdentifier("app"), new SqlIdentifier(name));

    [Fact]
    public void Parse_RenameSchema_TakesBareNames()
        => Directives("CREATE SCHEMA core; RENAME SCHEMA sales TO core;")
            .Schemas.Renames.ShouldHaveSingleItem()
            .ShouldBe(new SchemaRename(new SqlIdentifier("sales"), new SqlIdentifier("core")));

    [Fact]
    public void Parse_RenameTable_TakesQualifiedFromAndBareTo()
        => Directives("CREATE SCHEMA app; CREATE TABLE app.people ( id int NOT NULL ); RENAME TABLE app.users TO people;")
            .Tables.Renames.ShouldHaveSingleItem()
            .ShouldBe(new ObjectRename(App("users"), new SqlIdentifier("people")));

    [Fact]
    public void Parse_RenameColumn_TakesAThreePartPath()
        => Directives("CREATE SCHEMA app; CREATE TABLE app.users ( full_name text NOT NULL ); RENAME COLUMN app.users.name TO full_name;")
            .Tables.ColumnRenames.ShouldHaveSingleItem()
            .ShouldBe(new MemberRename(
                new MemberReference(new SqlIdentifier("app"), new SqlIdentifier("users"), new SqlIdentifier("name")),
                new SqlIdentifier("full_name")));

    [Fact]
    public void Parse_RenameMaterializedView_IsAViewRename()
        => Directives("CREATE SCHEMA app; CREATE MATERIALIZED VIEW app.daily AS SELECT 1 FROM app.t; RENAME MATERIALIZED VIEW app.old_daily TO daily;")
            .Views.Renames.ShouldHaveSingleItem().From.Name.ShouldBe("old_daily");

    [Theory]
    [InlineData("FUNCTION")]
    [InlineData("PROCEDURE")]
    [InlineData("ROUTINE")]
    public void Parse_RenameRoutineSpellings_AllRenameARoutine(string keyword)
        => Directives($"CREATE SCHEMA app; CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$; RENAME {keyword} app.old_f TO f;")
            .Routines.Renames.ShouldHaveSingleItem().From.Name.ShouldBe("old_f");

    [Fact]
    public void Parse_PartialSchema_MarksTheDeclaredSchema()
        => Directives("CREATE SCHEMA app; PARTIAL SCHEMA app;")
            .Schemas.Partials.ShouldHaveSingleItem().ShouldBe("app");

    [Fact]
    public void Parse_RepeatedDrop_IsIdempotent()
        => Directives("CREATE SCHEMA app; DROP TABLE app.old; DROP TABLE app.old;")
            .Tables.Drops.ShouldHaveSingleItem();

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_RenameTableUnqualified_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("RENAME TABLE users TO people;").Parse())
            .Message.ShouldContain("Expected '.'");

    [Fact]
    public void Parse_RenameColumnTwoPartPath_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("RENAME COLUMN users.name TO full_name;").Parse())
            .Message.ShouldContain("'.' in the column path");

    [Fact]
    public void Parse_RenameMissingTo_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("RENAME TABLE app.users people;").Parse())
            .Message.ShouldContain("Expected 'TO'");

    [Fact]
    public void Parse_RenameUnknownKind_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("RENAME GRANT app.x TO y;").Parse())
            .Message.ShouldContain("Expected a renameable kind");

    [Fact]
    public void Parse_PartialOfNonSchema_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("PARTIAL TABLE app.users;").Parse())
            .Message.ShouldContain("Expected 'SCHEMA'");

    [Fact]
    public void Parse_ObjectRenameInsideTemplate_BindsPerAppliedSchema()
    {
        // An unqualified object rename in a template body binds to each applied schema.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE t BEGIN CREATE TABLE people ( id int NOT NULL ); RENAME TABLE staff TO people; END;
            APPLY TEMPLATE t IN SCHEMA sales, billing;
            """).Parse();

        project.Directives.Tables.Renames.Select(r => (r.From.ToString(), r.To.Value))
            .ShouldBe([("sales.staff", "people"), ("billing.staff", "people")]);
    }

    [Fact]
    public void Parse_ColumnRenameInsideTemplate_UsesTwoPartPathBoundPerSchema()
    {
        // Inside a template a column path is table.column; the schema binds to each applied schema.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE t BEGIN CREATE TABLE orders ( total int NOT NULL ); RENAME COLUMN orders.amount TO total; END;
            APPLY TEMPLATE t IN SCHEMA sales, billing;
            """).Parse();

        project.Directives.Tables.ColumnRenames.Select(r => (r.From.ToString(), r.To.Value))
            .ShouldBe([("sales.orders.amount", "total"), ("billing.orders.amount", "total")]);
    }

    [Fact]
    public void Parse_SchemaRenameInsideTemplate_IsRejected()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser(
            "TEMPLATE t BEGIN RENAME SCHEMA a TO c; END;").Parse())
            .Message.ShouldContain("not allowed in a template body");
}
