using System.Text;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.State;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlWriterTests
{
    private static string WriteOneTable(Table table)
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app", Tables: [table])]));

    // Canonicalize a schema to a deterministic string for structural-equality comparison,
    // using the internal state serializer (independent of the DDL writer under test).
    private static string Canonical(DatabaseSchema schema)
        => Encoding.UTF8.GetString(new SchemaStateSerializer().Serialize(schema).Span);

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
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app", OldName: "legacy", IsPartial: true)]))
            .ShouldContain("CREATE PARTIAL SCHEMA app RENAMED FROM legacy;");

    [Fact]
    public void Write_TableGrant_IsEmitted()
        => WriteOneTable(new Table("t", Columns: [new Column("id", SqlType.Int)],
            Grants: [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)]))
            .ShouldContain("GRANT SELECT, INSERT ON app.t TO readers;");

    [Fact]
    public void Write_SchemaGrant_IsEmitted()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app", Grants: [new SchemaGrant("app_role")])]))
            .ShouldContain("GRANT USAGE ON SCHEMA app TO app_role;");

    [Fact]
    public void Write_Drops_AreEmitted()
    {
        var ddl = DdlWriter.Instance.Write(new DatabaseSchema(
            [new SchemaDefinition("app", DroppedTables: ["old_table"])],
            DroppedSchemas: ["scratch"]));
        ddl.ShouldContain("DROP TABLE app.old_table;");
        ddl.ShouldContain("DROP SCHEMA scratch;");
    }

    // -------------------------------------------------------------------------
    // Round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_ThenParse_PreservesModelStructurally()
    {
        var original = TestData.RichSchema();
        var reparsed = DdlReader.Instance.Read(DdlWriter.Instance.Write(original)).Schema;
        Canonical(reparsed).ShouldBe(Canonical(original));
    }

    [Fact]
    public void Write_IsStableThroughParseRoundTrip()
    {
        var ddl = DdlWriter.Instance.Write(TestData.RichSchema());
        var reEmitted = DdlWriter.Instance.Write(DdlReader.Instance.Read(ddl).Schema);
        reEmitted.ShouldBe(ddl);
    }

    [Fact]
    public Task Write_RichSchema_MatchesSnapshot() => Verify(DdlWriter.Instance.Write(TestData.RichSchema()));

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_View_EmitsCreateViewWithBody()
    {
        var schema = new DatabaseSchema([
            new SchemaDefinition("app", Views: [new View("active", "SELECT id FROM app.users WHERE active")]),
        ]);
        DdlWriter.Instance.Write(schema).ShouldContain("CREATE VIEW app.active AS SELECT id FROM app.users WHERE active;");
    }

    [Fact]
    public void Write_View_RoundTripsThroughParse()
    {
        var source = "CREATE SCHEMA app;\n\nCREATE VIEW app.active AS SELECT id, name FROM app.users WHERE active;\n";
        var reEmitted = DdlWriter.Instance.Write(DdlReader.Instance.Read(source).Schema);
        var reparsed = DdlReader.Instance.Read(reEmitted).Schema;

        var view = reparsed.Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();
        view.Name.ShouldBe("active");
        view.Body.ShouldBe("SELECT id, name FROM app.users WHERE active");
        view.DependsOn.ShouldHaveSingleItem().ShouldBe(new ViewDependency("app", "users"));
    }

    // -------------------------------------------------------------------------
    // Enums and sequences
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_Enum_EmitsQuotedValueList()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Enums: [new EnumType("status", ["pending", "shipped"])]),
        ])).ShouldContain("CREATE ENUM app.status ('pending', 'shipped');");

    [Fact]
    public void Write_EnumValueWithQuote_EscapesIt()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Enums: [new EnumType("status", ["it's"])]),
        ])).ShouldContain("CREATE ENUM app.status ('it''s');");

    [Fact]
    public void Write_Sequence_WithoutOptions_OmitsParens()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Sequences: [new Sequence("order_id")]),
        ])).ShouldContain("CREATE SEQUENCE app.order_id;");

    [Fact]
    public void Write_Sequence_EmitsOptionsInCanonicalOrder()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Sequences:
            [
                new Sequence("order_id", new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5,
                    MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true)),
            ]),
        ])).ShouldContain("CREATE SEQUENCE app.order_id (AS bigint, START 100, INCREMENT 5, MINVALUE -10, MAXVALUE 999999, CACHE 10, CYCLE);");

    [Fact]
    public void Write_EnumAndSequenceDrops_AreEmitted()
    {
        var ddl = DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", DroppedEnums: ["stale_enum"], DroppedSequences: ["stale_seq"]),
        ]));
        ddl.ShouldContain("DROP ENUM app.stale_enum;");
        ddl.ShouldContain("DROP SEQUENCE app.stale_seq;");
    }

    // -------------------------------------------------------------------------
    // Functions and procedures
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_Function_EmitsArgumentsAndDefinitionVerbatim()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Functions:
                [new Function("add_tax", "amount numeric", "RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$")]),
        ])).ShouldContain("CREATE FUNCTION app.add_tax(amount numeric) RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$;");

    [Fact]
    public void Write_Function_MultiLineDefinition_KeepsNewlines()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Functions:
                [new Function("f", "", "RETURNS int LANGUAGE sql AS $$\n  SELECT 1;\n$$")]),
        ])).ShouldContain("CREATE FUNCTION app.f() RETURNS int LANGUAGE sql AS $$\n  SELECT 1;\n$$;");

    [Fact]
    public void Write_Function_TrailingWhitespaceInDefinition_IsTrimmed()
        // A code-built definition ending in whitespace must not push the ';' onto dangling whitespace.
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Functions: [new Function("f", "", "RETURNS int AS $$ SELECT 1 $$  \n")]),
        ])).ShouldContain("AS $$ SELECT 1 $$;");

    [Fact]
    public void Write_Procedure_IsEmitted()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Procedures: [new Procedure("archive", "before date", "LANGUAGE sql AS $$ DELETE $$")]),
        ])).ShouldContain("CREATE PROCEDURE app.archive(before date) LANGUAGE sql AS $$ DELETE $$;");

    [Fact]
    public void Write_FunctionAndProcedureDrops_AreEmitted()
    {
        var ddl = DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", DroppedFunctions: ["stale_fn"], DroppedProcedures: ["stale_proc"]),
        ]));
        ddl.ShouldContain("DROP FUNCTION app.stale_fn;");
        ddl.ShouldContain("DROP PROCEDURE app.stale_proc;");
    }
}
