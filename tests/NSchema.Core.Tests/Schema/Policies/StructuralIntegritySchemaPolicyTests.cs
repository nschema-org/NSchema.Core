using NSchema.Diagnostics;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.CompositeTypes;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Views;
using NSchema.Schema.Policies;

namespace NSchema.Tests.Schema.Policies;

public sealed class StructuralIntegritySchemaPolicyTests
{
    private readonly StructuralIntegritySchemaPolicy _sut = new();

    private static Column Col(string name) => new Column(name, SqlType.BigInt);

    private static DatabaseSchema Db(params Table[] tables) =>
        new DatabaseSchema([new SchemaDefinition("public", Tables: tables)]);

    [Fact]
    public void NoDiagnostics_ForAConsistentSchema()
    {
        // Arrange
        var users = new Table("users", PrimaryKey: new PrimaryKey("users_pk", ["id"]), Columns: [Col("id")]);
        var orders = new Table(
            "orders",
            PrimaryKey: new PrimaryKey("orders_pk", ["id"]),
            Columns: [Col("id"), Col("user_id")],
            ForeignKeys: [new ForeignKey("orders_users_fk", ["user_id"], "public", "users", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(users, orders)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenIndexNameIsReusedAcrossTablesInASchema()
    {
        // Arrange — index names are schema-scoped, so two tables can't both declare 'ix_updated_at'.
        var invoices = new Table("invoices", Columns: [Col("updated_at")],
            Indexes: [new TableIndex("ix_updated_at", [new IndexColumn("updated_at")])]);
        var orders = new Table("orders", Columns: [Col("updated_at")],
            Indexes: [new TableIndex("ix_updated_at", [new IndexColumn("updated_at")])]);

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain("ix_updated_at");
        diagnostic.Message.ShouldContain("public.invoices");
        diagnostic.Message.ShouldContain("public.orders");
    }

    [Fact]
    public void Error_WhenAPrimaryKeyNameCollidesWithAnIndexName()
    {
        // Arrange — a PRIMARY KEY creates an index bearing its name, so it shares the schema-wide pool.
        var invoices = new Table("invoices", PrimaryKey: new PrimaryKey("shared_name", ["id"]), Columns: [Col("id")]);
        var orders = new Table("orders", Columns: [Col("id")],
            Indexes: [new TableIndex("shared_name", [new IndexColumn("id")])]);

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldContain("shared_name");
    }

    [Fact]
    public void NoDiagnostics_WhenTheSameIndexNameIsUsedInDifferentSchemas()
    {
        // Arrange
        var table = new Table("invoices", Columns: [Col("updated_at")],
            Indexes: [new TableIndex("ix_updated_at", [new IndexColumn("updated_at")])]);
        var schema = new DatabaseSchema(
        [
            new SchemaDefinition("billing", Tables: [table]),
            new SchemaDefinition("ordering", Tables: [table]),
        ]);

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenTableHasNoColumns()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table("empty"))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("no columns"));
    }

    [Fact]
    public void Error_WhenColumnDeclaredTwice()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table("t", Columns: [Col("id"), Col("ID")]))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void Error_WhenPrimaryKeyReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table("t", PrimaryKey: new PrimaryKey("pk", ["missing"]), Columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Primary key") && d.Message.Contains("missing"));
    }

    [Fact]
    public void Error_WhenIndexReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table("t", Columns: [Col("id")], Indexes: [new TableIndex("ix", ["nope"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Index") && d.Message.Contains("nope"));
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesUnknownLocalColumn()
    {
        // Arrange
        var table = new Table(
            "t", Columns: [Col("id")],
            ForeignKeys: [new ForeignKey("fk", ["ghost"], "public", "t", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("unknown local column 'ghost'"));
    }

    [Fact]
    public void Error_WhenForeignKeyArityMismatches()
    {
        // Arrange
        var table = new Table(
            "t",
            PrimaryKey: new PrimaryKey("pk", ["id"]),
            Columns: [Col("id"), Col("a"), Col("b")],
            ForeignKeys: [new ForeignKey("fk", ["a", "b"], "public", "t", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("counts must match"));
    }

    [Fact]
    public void Error_WhenForeignKeyTargetTableMissingInManagedSchema()
    {
        // Arrange
        var table = new Table(
            "t", Columns: [Col("id"), Col("ref")],
            ForeignKeys: [new ForeignKey("fk", ["ref"], "public", "absent", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("unknown table 'public.absent'"));
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetMissingInPartialSchema()
    {
        // Arrange
        var table = new Table(
            "t", Columns: [Col("id"), Col("ref")],
            ForeignKeys: [new ForeignKey("fk", ["ref"], "public", "absent", ["id"])]);
        var schema = new DatabaseSchema([new SchemaDefinition("public", IsPartial: true, Tables: [table])]);

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetSchemaIsUnmanaged()
    {
        // Arrange — "external" schema is not present in the document at all.
        var table = new Table(
            "t", Columns: [Col("id"), Col("ref")],
            ForeignKeys: [new ForeignKey("fk", ["ref"], "external", "other", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesNonUniqueColumns()
    {
        // Arrange — target column exists but is neither a PK nor a unique index.
        var target = new Table("target", PrimaryKey: new PrimaryKey("pk", ["id"]), Columns: [Col("id"), Col("code")]);
        var source = new Table(
            "source", Columns: [Col("id"), Col("code")],
            ForeignKeys: [new ForeignKey("fk", ["code"], "public", "target", ["code"])]);

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void NoError_WhenForeignKeyReferencesUniqueIndex()
    {
        // Arrange
        var target = new Table(
            "target",
            PrimaryKey: new PrimaryKey("pk", ["id"]),
            Columns: [Col("id"), Col("code")],
            Indexes: [new TableIndex("uq", ["code"], IsUnique: true)]);
        var source = new Table(
            "source", Columns: [Col("id"), Col("code")],
            ForeignKeys: [new ForeignKey("fk", ["code"], "public", "target", ["code"])]);

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesPredicatedUniqueIndex()
    {
        // Arrange — a partial (predicated) unique index cannot back a foreign key.
        var target = new Table(
            "target",
            PrimaryKey: new PrimaryKey("pk", ["id"]),
            Columns: [Col("id"), Col("code")],
            Indexes: [new TableIndex("uq", ["code"], IsUnique: true, Predicate: "code IS NOT NULL")]);
        var source = new Table(
            "source", Columns: [Col("id"), Col("code")],
            ForeignKeys: [new ForeignKey("fk", ["code"], "public", "target", ["code"])]);

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void Error_WhenFunctionAndProcedureShareAName()
    {
        // Arrange — the parser and aggregation enforce this for parsed schemas; the policy is the catch-all
        // for JSON-sourced and code-built schemas.
        var schema = new DatabaseSchema([
            new SchemaDefinition("public", Routines:
            [
                new Routine("r", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 1 $$"),
                new Routine("r", RoutineKind.Procedure, "", "AS $$ SELECT 1 $$"),
            ]),
        ]);

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("share a single name space"));
    }

    [Fact]
    public void Error_WhenFunctionDeclaredTwice()
    {
        // Arrange
        var schema = new DatabaseSchema([
            new SchemaDefinition("public", Routines:
            [
                new Routine("f", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 1 $$"),
                new Routine("f", RoutineKind.Function, "a int", "RETURNS int AS $$ SELECT 2 $$"),
            ]),
        ]);

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert — overloading is not supported: one routine per name.
        diagnostics.ShouldContain(d => d.Message.Contains("declares routine 'f' more than once"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossObjectKinds()
    {
        // A table and a view called 'foo' cannot coexist — they share one name space in the database.
        var schema = new DatabaseSchema([
            new SchemaDefinition("public",
                Tables: [new Table("foo", Columns: [Col("id")])],
                Views: [new View("foo", "SELECT 1")]),
        ]);

        var diagnostics = _sut.Validate(schema).ToList();

        diagnostics.ShouldContain(d => d.Message.Contains("reuses the name 'foo'") && d.Message.Contains("table") && d.Message.Contains("view"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossTableAndEnum()
    {
        // Relations and types share pg_type (a relation has a row type), so a table and an enum collide too.
        var schema = new DatabaseSchema([
            new SchemaDefinition("public",
                Tables: [new Table("status", Columns: [Col("id")])],
                Enums: [new EnumType("status", ["a", "b"])]),
        ]);

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("reuses the name 'status'"));
    }

    [Fact]
    public void Error_WhenSequenceDeclaredTwice()
    {
        var schema = new DatabaseSchema([
            new SchemaDefinition("public", Sequences: [new Sequence("seq"), new Sequence("seq")]),
        ]);

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("declares sequence 'seq' more than once"));
    }

    [Fact]
    public void NoDiagnostics_WhenNamesDifferAcrossKinds()
    {
        var schema = new DatabaseSchema([
            new SchemaDefinition("public",
                Tables: [new Table("t", Columns: [Col("id")])],
                Views: [new View("v", "SELECT 1")],
                Sequences: [new Sequence("s")],
                CompositeTypes: [new CompositeType("c", [new CompositeField("f", SqlType.Int)])],
                Enums: [new EnumType("e", ["a"])],
                Domains: [new Domain("d", SqlType.Text)]),
        ]);

        _sut.Validate(schema).ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenColumnHasBothDefaultAndGenerated()
    {
        var table = new Table("t", Columns: [new Column("area", SqlType.Int, DefaultExpression: "0", GeneratedExpression: "w * h")]);

        _sut.Validate(Db(table)).ShouldContain(d =>
            d.Message.Contains("both a DEFAULT and a GENERATED") && d.Message.Contains("area"));
    }

    [Fact]
    public void NoDiagnostics_ForGeneratedColumnWithoutDefault()
    {
        var table = new Table("t",
            PrimaryKey: new PrimaryKey("t_pk", ["id"]),
            Columns: [Col("id"), new Column("area", SqlType.Int, GeneratedExpression: "w * h")]);

        _sut.Validate(Db(table)).ShouldBeEmpty();
    }
}
