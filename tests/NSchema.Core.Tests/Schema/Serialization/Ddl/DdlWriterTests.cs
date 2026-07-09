using System.Text;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.CompositeTypes;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;
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
    public void Write_WithoutSchemaDeclarations_EmitsOnlyMemberObjects()
    {
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("t", Columns: [new Column("id", SqlType.Int)])])]);

        var ddl = DdlWriter.Instance.Write(schema, declareSchemas: false);

        ddl.ShouldNotContain("CREATE SCHEMA");
        ddl.ShouldStartWith("CREATE TABLE app.t");
    }

    [Fact]
    public void Write_WithoutSchemaDeclarations_RoundTripsThroughParse()
    {
        // The reader vivifies the schema from the objects' qualified names, so a declaration-free file
        // reads back to the same members.
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("t", Columns: [new Column("id", SqlType.Int)])],
            Views: [new View("active", "SELECT 1")])]);

        var reparsed = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema, declareSchemas: false)).Schema;

        var app = reparsed.Schemas.ShouldHaveSingleItem();
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("t");
        app.Views.ShouldHaveSingleItem().Name.ShouldBe("active");
    }

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
    // Triggers
    // -------------------------------------------------------------------------

    private static string WriteTriggerOn(Trigger trigger)
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("id", SqlType.Int)], Triggers: [trigger])])]));

    [Fact]
    public void Write_Trigger_EmitsStandaloneCreateTriggerAfterTable()
    {
        var ddl = WriteTriggerOn(new Trigger("audit", TriggerTiming.After,
            TriggerEvent.Insert | TriggerEvent.Update, "app.log", TriggerLevel.Row));
        ddl.ShouldContain("CREATE TRIGGER audit AFTER INSERT OR UPDATE ON app.users FOR EACH ROW EXECUTE FUNCTION app.log();");
    }

    [Fact]
    public void Write_TriggerWithUpdateOfWhenAndComment_IsEmitted()
    {
        var ddl = WriteTriggerOn(new Trigger("audit", TriggerTiming.After, TriggerEvent.Update, "app.log",
            TriggerLevel.Row, UpdateOfColumns: ["email"], When: "new.email IS NOT NULL", Comment: "audit"));
        ddl.ShouldContain("--- audit\nCREATE TRIGGER audit AFTER UPDATE OF (email) ON app.users FOR EACH ROW WHEN (new.email IS NOT NULL) EXECUTE FUNCTION app.log();");
    }

    [Fact]
    public void Write_InsteadOfTrigger_IsEmitted()
        => WriteTriggerOn(new Trigger("v_ins", TriggerTiming.InsteadOf, TriggerEvent.Insert, "app.f", TriggerLevel.Row))
            .ShouldContain("CREATE TRIGGER v_ins INSTEAD OF INSERT ON app.users FOR EACH ROW EXECUTE FUNCTION app.f();");

    [Fact]
    public void Write_Trigger_RoundTripsThroughParse()
    {
        var trigger = new Trigger("audit", TriggerTiming.After, TriggerEvent.Insert | TriggerEvent.Delete, "app.log",
            TriggerLevel.Row, When: "true", FunctionArguments: "'x'", Comment: "note");
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("id", SqlType.Int)], Triggers: [trigger])])]);

        var reparsed = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema;
        var roundTripped = reparsed.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem();
        roundTripped.ShouldBe(trigger);            // structural equality (excludes the comment)
        roundTripped.Comment.ShouldBe("note");     // ... so assert the comment round-tripped too
    }

    [Fact]
    public void Write_InlineBodyTrigger_EmitsDollarQuotedBody()
        => WriteTriggerOn(new Trigger("audit", TriggerTiming.After, TriggerEvent.Insert,
                Body: "BEGIN INSERT INTO app.log VALUES (1); END"))
            .ShouldContain("CREATE TRIGGER audit AFTER INSERT ON app.users AS $$\nBEGIN INSERT INTO app.log VALUES (1); END\n$$;");

    [Fact]
    public void Write_InlineBodyTrigger_RoundTripsThroughParse()
    {
        // A body with its own semicolons survives because it is emitted (and lexed) as one dollar-quoted block.
        var trigger = new Trigger("audit", TriggerTiming.InsteadOf, TriggerEvent.Insert | TriggerEvent.Update,
            Body: "BEGIN\n  UPDATE app.users SET id = id;\n  INSERT INTO app.log VALUES (1);\nEND", Comment: "note");
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("id", SqlType.Int)], Triggers: [trigger])])]);

        var reparsed = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema;
        var roundTripped = reparsed.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem();
        roundTripped.ShouldBe(trigger);
        roundTripped.Body.ShouldBe(trigger.Body);
    }

    // -------------------------------------------------------------------------
    // Extensions (database-global, root-level)
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_Extension_EmitsCreateExtension()
        => DdlWriter.Instance.Write(new DatabaseSchema(Extensions: [new Extension("citext")]))
            .ShouldContain("CREATE EXTENSION citext;");

    [Fact]
    public void Write_ExtensionWithVersion_EmitsVersionClause()
        => DdlWriter.Instance.Write(new DatabaseSchema(Extensions: [new Extension("postgis", Version: "3.4")]))
            .ShouldContain("CREATE EXTENSION postgis VERSION '3.4';");

    [Fact]
    public void Write_ExtensionWithNonIdentifierName_QuotesIt()
        // A hyphenated name (e.g. uuid-ossp) must be quoted so it round-trips through the parser.
        => DdlWriter.Instance.Write(new DatabaseSchema(Extensions: [new Extension("uuid-ossp")]))
            .ShouldContain("CREATE EXTENSION 'uuid-ossp';");

    [Fact]
    public void Write_ExtensionComment_EmitsDocComment()
        => DdlWriter.Instance.Write(new DatabaseSchema(Extensions: [new Extension("postgis", Comment: "spatial types")]))
            .ShouldContain("--- spatial types\nCREATE EXTENSION postgis;");

    [Fact]
    public void Write_DroppedExtension_IsEmitted()
        => DdlWriter.Instance.Write(new DatabaseSchema(DroppedExtensions: ["stale_ext"]))
            .ShouldContain("DROP EXTENSION stale_ext;");

    [Fact]
    public void Write_Extension_RoundTripsThroughParse()
    {
        var schema = new DatabaseSchema(Extensions:
            [new Extension("citext"), new Extension("uuid-ossp", Comment: "ids"), new Extension("postgis", Version: "3.4")]);
        var reparsed = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema;
        reparsed.Extensions.ShouldBe(schema.Extensions);
    }

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
    // Domains
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_SimpleDomain_EmitsCreateDomain()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app",
            Domains: [new Domain("typeid", SqlType.Text)])]))
            .ShouldContain("CREATE DOMAIN app.typeid AS text;");

    [Fact]
    public void Write_DomainWithEveryClause_EmitsInCanonicalOrder()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app",
            Domains: [new Domain("email", SqlType.Text, Default: "'x@y'", NotNull: true,
                Checks: [new CheckConstraint("email_fmt", "VALUE ~ '@'")])])]))
            // NOT NULL, then checks, then DEFAULT (last, so its opaque expr reads back to the ';').
            .ShouldContain("CREATE DOMAIN app.email AS text NOT NULL CONSTRAINT email_fmt CHECK (VALUE ~ '@') DEFAULT 'x@y';");

    [Fact]
    public void Write_DroppedDomain_IsEmitted()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app", DroppedDomains: ["stale"])]))
            .ShouldContain("DROP DOMAIN app.stale;");

    [Fact]
    public void Write_Domain_RoundTripsThroughParse()
    {
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Domains: [new Domain("email", SqlType.Text, Default: "'x@y'", NotNull: true,
                Checks: [new CheckConstraint("email_fmt", "VALUE ~ '@'")], OldName: "addr", Comment: "an email")])]);

        var domain = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema
            .Schemas.ShouldHaveSingleItem().Domains.ShouldHaveSingleItem();
        domain.DataType.ShouldBe(SqlType.Text);
        domain.NotNull.ShouldBeTrue();
        domain.Default.ShouldBe("'x@y'");
        domain.Checks.ShouldHaveSingleItem().Name.ShouldBe("email_fmt");
    }

    // -------------------------------------------------------------------------
    // Composite types
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_SimpleCompositeType_EmitsCreateType()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app",
            CompositeTypes: [new CompositeType("address", [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)])])]))
            .ShouldContain("CREATE TYPE app.address AS (street text, zip int);");

    [Fact]
    public void Write_DroppedCompositeType_IsEmitted()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app", DroppedCompositeTypes: ["stale"])]))
            .ShouldContain("DROP TYPE app.stale;");

    [Fact]
    public void Write_CompositeType_RoundTripsThroughParse()
    {
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            CompositeTypes: [new CompositeType("address", [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)],
                OldName: "legacy_address", Comment: "a postal address")])]);

        var type = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema
            .Schemas.ShouldHaveSingleItem().CompositeTypes.ShouldHaveSingleItem();
        type.Name.ShouldBe("address");
        type.Fields.Count.ShouldBe(2);
        type.Fields[0].DataType.ShouldBe(SqlType.Text);
        type.Fields[1].DataType.ShouldBe(SqlType.Int);
    }

    // -------------------------------------------------------------------------
    // Materialized views
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_MaterializedView_EmitsMaterializedKeyword()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app",
            Views: [new View("daily", "SELECT 1", IsMaterialized: true)])]))
            .ShouldContain("CREATE MATERIALIZED VIEW app.daily AS SELECT 1;");

    [Fact]
    public void Write_MaterializedViewIndex_EmitsStandaloneCreateIndex()
        => DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app",
            Views: [new View("daily", "SELECT x FROM app.t", IsMaterialized: true,
                Indexes: [new TableIndex("daily_ix", ["x"], IsUnique: true, Predicate: "x IS NOT NULL")])])]))
            .ShouldContain("CREATE UNIQUE INDEX daily_ix ON app.daily (x) WHERE (x IS NOT NULL);");

    [Fact]
    public void Write_MaterializedView_RoundTripsThroughParse()
    {
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Views: [new View("daily", "SELECT x FROM app.t", IsMaterialized: true,
                Indexes: [new TableIndex("daily_ix", ["x"])])])]);

        var view = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema
            .Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();
        view.IsMaterialized.ShouldBeTrue();
        view.Indexes.ShouldHaveSingleItem().Name.ShouldBe("daily_ix");
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
            new SchemaDefinition("app", Routines:
                [new Routine("add_tax", RoutineKind.Function, "amount numeric", "RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$")]),
        ])).ShouldContain("CREATE FUNCTION app.add_tax(amount numeric) RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$;");

    [Fact]
    public void Write_Function_MultiLineDefinition_KeepsNewlines()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Routines:
                [new Routine("f", RoutineKind.Function, "", "RETURNS int LANGUAGE sql AS $$\n  SELECT 1;\n$$")]),
        ])).ShouldContain("CREATE FUNCTION app.f() RETURNS int LANGUAGE sql AS $$\n  SELECT 1;\n$$;");

    [Fact]
    public void Write_Function_TrailingWhitespaceInDefinition_IsTrimmed()
        // A code-built definition ending in whitespace must not push the ';' onto dangling whitespace.
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Routines: [new Routine("f", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 1 $$  \n")]),
        ])).ShouldContain("AS $$ SELECT 1 $$;");

    [Fact]
    public void Write_Procedure_IsEmitted()
        => DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", Routines: [new Routine("archive", RoutineKind.Procedure, "before date", "LANGUAGE sql AS $$ DELETE $$")]),
        ])).ShouldContain("CREATE PROCEDURE app.archive(before date) LANGUAGE sql AS $$ DELETE $$;");

    [Fact]
    public void Write_RoutineDrops_AreEmitted()
    {
        // Routines are recorded by name only (one name space), so they are emitted with kind-agnostic DROP ROUTINE.
        var ddl = DdlWriter.Instance.Write(new DatabaseSchema([
            new SchemaDefinition("app", DroppedRoutines: ["stale_fn", "stale_proc"]),
        ]));
        ddl.ShouldContain("DROP ROUTINE app.stale_fn;");
        ddl.ShouldContain("DROP ROUTINE app.stale_proc;");
    }
}
