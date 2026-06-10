using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;
using NSchema.Schema.Serialization.Ddl;
using NSchema.Schema.Serialization.Json;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlSchemaWriterTests
{
    private static string WriteOneTable(Table table)
        => DdlSchemaWriter.Write(new DatabaseSchema([new SchemaDefinition("app", Tables: [table])]));

    private static async Task<string> Json(DatabaseSchema schema)
    {
        using var stream = new MemoryStream();
        await JsonSchemaSerializer.Instance.Write(schema, stream, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // -------------------------------------------------------------------------
    // Columns
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_NotNullColumn_EmitsNotNull()
        => WriteOneTable(new Table("t", Columns: [new Column("id", SqlType.Int)])).ShouldContain("id int NOT NULL");

    [Fact]
    public void Write_NullableColumn_OmitsNullKeyword()
    {
        var ddl = WriteOneTable(new Table("t", Columns: [new Column("note", SqlType.Text, IsNullable: true)]));
        ddl.ShouldContain("note text");
        ddl.ShouldNotContain("note text NULL");
        ddl.ShouldNotContain("note text NOT NULL");
    }

    [Fact]
    public void Write_IdentityWithOptions_EmitsInStartIncrementMinValueOrder()
        => WriteOneTable(new Table("t", Columns:
            [new Column("id", SqlType.BigInt, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 5, 2))]))
            .ShouldContain("id bigint NOT NULL IDENTITY (START 1, INCREMENT 2, MINVALUE 5)");

    [Fact]
    public void Write_BareIdentity_EmitsNoParens()
        => WriteOneTable(new Table("t", Columns: [new Column("id", SqlType.BigInt, IsIdentity: true)]))
            .ShouldContain("id bigint NOT NULL IDENTITY\n");

    [Fact]
    public void Write_DefaultAndRename_AreEmitted()
        => WriteOneTable(new Table("t", Columns:
            [new Column("flag", SqlType.Int, DefaultExpression: "0", OldName: "legacy_flag")]))
            .ShouldContain("flag int NOT NULL DEFAULT 0 RENAMED FROM legacy_flag");

    [Fact]
    public void Write_ParameterisedType_RendersFacets()
        => WriteOneTable(new Table("t", Columns: [new Column("amount", SqlType.Decimal(18, 2))]))
            .ShouldContain("amount decimal(18,2)");

    // -------------------------------------------------------------------------
    // Constraints and indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_PrimaryKey_IsEmitted()
        => WriteOneTable(new Table("t", Columns: [new Column("id", SqlType.Int)], PrimaryKey: new PrimaryKey("t_pk", ["id"])))
            .ShouldContain("CONSTRAINT t_pk PRIMARY KEY (id)");

    [Fact]
    public void Write_ForeignKeyWithActions_IsEmitted()
        => WriteOneTable(new Table("orders", Columns: [new Column("user_id", SqlType.Int)],
            ForeignKeys: [new ForeignKey("fk", ["user_id"], "app", "users", ["id"], ReferentialAction.Cascade, ReferentialAction.SetNull)]))
            .ShouldContain("CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES app.users (id) ON DELETE CASCADE ON UPDATE SET NULL");

    [Fact]
    public void Write_ForeignKeyWithoutActions_OmitsOnClauses()
        => WriteOneTable(new Table("orders", Columns: [new Column("user_id", SqlType.Int)],
            ForeignKeys: [new ForeignKey("fk", ["user_id"], "app", "users", ["id"])]))
            .ShouldNotContain("ON DELETE");

    [Fact]
    public void Write_Check_WrapsExpressionInParens()
        => WriteOneTable(new Table("t", Columns: [new Column("age", SqlType.Int)],
            CheckConstraints: [new CheckConstraint("chk", "age >= 0")]))
            .ShouldContain("CONSTRAINT chk CHECK (age >= 0)");

    [Fact]
    public void Write_PartialUniqueIndex_IsEmitted()
        => WriteOneTable(new Table("t", Columns: [new Column("email", SqlType.Text)],
            Indexes: [new TableIndex("ux", ["email"], IsUnique: true, Predicate: "deleted_at IS NULL")]))
            .ShouldContain("UNIQUE INDEX ux (email) WHERE (deleted_at IS NULL)");

    // -------------------------------------------------------------------------
    // Comments, schemas, grants, drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_ColumnComment_EmitsDocComment()
        => WriteOneTable(new Table("t", Columns: [new Column("email", SqlType.Text, Comment: "Primary contact.")]))
            .ShouldContain("--- Primary contact.\n");

    [Fact]
    public void Write_MultiLineComment_EmitsOneDocLinePerLine()
        => WriteOneTable(new Table("t", Comment: "Line one.\nLine two.", Columns: [new Column("id", SqlType.Int)]))
            .ShouldContain("--- Line one.\n--- Line two.\n");

    [Fact]
    public void Write_PartialSchemaWithRename_IsEmitted()
        => DdlSchemaWriter.Write(new DatabaseSchema([new SchemaDefinition("app", OldName: "legacy", IsPartial: true)]))
            .ShouldContain("CREATE PARTIAL SCHEMA app RENAMED FROM legacy;");

    [Fact]
    public void Write_TableGrant_IsEmitted()
        => WriteOneTable(new Table("t", Columns: [new Column("id", SqlType.Int)],
            Grants: [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)]))
            .ShouldContain("GRANT SELECT, INSERT ON app.t TO readers;");

    [Fact]
    public void Write_SchemaGrant_IsEmitted()
        => DdlSchemaWriter.Write(new DatabaseSchema([new SchemaDefinition("app", Grants: [new SchemaGrant("app_role")])]))
            .ShouldContain("GRANT USAGE ON SCHEMA app TO app_role;");

    [Fact]
    public void Write_Drops_AreEmitted()
    {
        var ddl = DdlSchemaWriter.Write(new DatabaseSchema(
            [new SchemaDefinition("app", DroppedTables: ["old_table"])],
            DroppedSchemas: ["scratch"]));
        ddl.ShouldContain("DROP TABLE app.old_table;");
        ddl.ShouldContain("DROP SCHEMA scratch;");
    }

    // -------------------------------------------------------------------------
    // Round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Write_ThenParse_PreservesModelStructurally()
    {
        var original = TestData.RichSchema();
        var reparsed = new DslParser(DdlSchemaWriter.Write(original)).Parse();
        (await Json(reparsed)).ShouldBe(await Json(original));
    }

    [Fact]
    public void Write_IsStableThroughParseRoundTrip()
    {
        var ddl = DdlSchemaWriter.Write(TestData.RichSchema());
        var reEmitted = DdlSchemaWriter.Write(new DslParser(ddl).Parse());
        reEmitted.ShouldBe(ddl);
    }

    [Fact]
    public Task Write_RichSchema_MatchesSnapshot() => Verify(DdlSchemaWriter.Write(TestData.RichSchema()));

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_View_EmitsCreateViewWithBody()
    {
        var schema = new DatabaseSchema([
            new SchemaDefinition("app", Views: [new View("active", "SELECT id FROM app.users WHERE active")]),
        ]);
        DdlSchemaWriter.Write(schema).ShouldContain("CREATE VIEW app.active AS SELECT id FROM app.users WHERE active;");
    }

    [Fact]
    public void Write_View_RoundTripsThroughParse()
    {
        var source = "CREATE SCHEMA app;\n\nCREATE VIEW app.active AS SELECT id, name FROM app.users WHERE active;\n";
        var reEmitted = DdlSchemaWriter.Write(new DslParser(source).Parse());
        var reparsed = new DslParser(reEmitted).Parse();

        var view = reparsed.Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();
        view.Name.ShouldBe("active");
        view.Body.ShouldBe("SELECT id, name FROM app.users WHERE active");
        view.DependsOn.ShouldHaveSingleItem().ShouldBe(new ViewDependency("app", "users"));
    }
}
