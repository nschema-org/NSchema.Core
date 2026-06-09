using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;
using NSchema.Schema.Serialization.Ddl;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlSchemaWriterTests
{
    private static string WriteOneTable(Table table)
        => DdlSchemaWriter.Write(DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [table])]));

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
        => WriteOneTable(Table.Create("t", columns: [Column.Create("id", SqlType.Int)])).ShouldContain("id int NOT NULL");

    [Fact]
    public void Write_NullableColumn_OmitsNullKeyword()
    {
        var ddl = WriteOneTable(Table.Create("t", columns: [Column.Create("note", SqlType.Text, isNullable: true)]));
        ddl.ShouldContain("note text");
        ddl.ShouldNotContain("note text NULL");
        ddl.ShouldNotContain("note text NOT NULL");
    }

    [Fact]
    public void Write_IdentityWithOptions_EmitsInStartIncrementMinValueOrder()
        => WriteOneTable(Table.Create("t", columns:
            [Column.Create("id", SqlType.BigInt, isIdentity: true, identityOptions: new IdentityOptions(1, 5, 2))]))
            .ShouldContain("id bigint NOT NULL IDENTITY (START 1, INCREMENT 2, MINVALUE 5)");

    [Fact]
    public void Write_BareIdentity_EmitsNoParens()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("id", SqlType.BigInt, isIdentity: true)]))
            .ShouldContain("id bigint NOT NULL IDENTITY\n");

    [Fact]
    public void Write_DefaultAndRename_AreEmitted()
        => WriteOneTable(Table.Create("t", columns:
            [Column.Create("flag", SqlType.Int, defaultExpression: "0", oldName: "legacy_flag")]))
            .ShouldContain("flag int NOT NULL DEFAULT 0 RENAMED FROM legacy_flag");

    [Fact]
    public void Write_ParameterisedType_RendersFacets()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("amount", SqlType.Decimal(18, 2))]))
            .ShouldContain("amount decimal(18,2)");

    // -------------------------------------------------------------------------
    // Constraints and indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_PrimaryKey_IsEmitted()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("id", SqlType.Int)], primaryKey: new PrimaryKey("t_pk", ["id"])))
            .ShouldContain("CONSTRAINT t_pk PRIMARY KEY (id)");

    [Fact]
    public void Write_ForeignKeyWithActions_IsEmitted()
        => WriteOneTable(Table.Create("orders", columns: [Column.Create("user_id", SqlType.Int)],
            foreignKeys: [ForeignKey.Create("fk", ["user_id"], "app", "users", ["id"], ReferentialAction.Cascade, ReferentialAction.SetNull)]))
            .ShouldContain("CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES app.users (id) ON DELETE CASCADE ON UPDATE SET NULL");

    [Fact]
    public void Write_ForeignKeyWithoutActions_OmitsOnClauses()
        => WriteOneTable(Table.Create("orders", columns: [Column.Create("user_id", SqlType.Int)],
            foreignKeys: [ForeignKey.Create("fk", ["user_id"], "app", "users", ["id"])]))
            .ShouldNotContain("ON DELETE");

    [Fact]
    public void Write_Check_WrapsExpressionInParens()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("age", SqlType.Int)],
            checkConstraints: [new CheckConstraint("chk", "age >= 0")]))
            .ShouldContain("CONSTRAINT chk CHECK (age >= 0)");

    [Fact]
    public void Write_PartialUniqueIndex_IsEmitted()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("email", SqlType.Text)],
            indexes: [TableIndex.Create("ux", ["email"], isUnique: true, predicate: "deleted_at IS NULL")]))
            .ShouldContain("UNIQUE INDEX ux (email) WHERE (deleted_at IS NULL)");

    // -------------------------------------------------------------------------
    // Comments, schemas, grants, drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_ColumnComment_EmitsDocComment()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("email", SqlType.Text, comment: "Primary contact.")]))
            .ShouldContain("--- Primary contact.\n");

    [Fact]
    public void Write_MultiLineComment_EmitsOneDocLinePerLine()
        => WriteOneTable(Table.Create("t", comment: "Line one.\nLine two.", columns: [Column.Create("id", SqlType.Int)]))
            .ShouldContain("--- Line one.\n--- Line two.\n");

    [Fact]
    public void Write_PartialSchemaWithRename_IsEmitted()
        => DdlSchemaWriter.Write(DatabaseSchema.Create([SchemaDefinition.Create("app", oldName: "legacy", isPartial: true)]))
            .ShouldContain("CREATE PARTIAL SCHEMA app RENAMED FROM legacy;");

    [Fact]
    public void Write_TableGrant_IsEmitted()
        => WriteOneTable(Table.Create("t", columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)]))
            .ShouldContain("GRANT SELECT, INSERT ON app.t TO readers;");

    [Fact]
    public void Write_SchemaGrant_IsEmitted()
        => DdlSchemaWriter.Write(DatabaseSchema.Create([SchemaDefinition.Create("app", grants: [new SchemaGrant("app_role")])]))
            .ShouldContain("GRANT USAGE ON SCHEMA app TO app_role;");

    [Fact]
    public void Write_Drops_AreEmitted()
    {
        var ddl = DdlSchemaWriter.Write(DatabaseSchema.Create(
            [SchemaDefinition.Create("app", droppedTables: ["old_table"])],
            droppedSchemas: ["scratch"]));
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
}
