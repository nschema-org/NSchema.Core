using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Policies;

namespace NSchema.Tests.Project.Policies;

public sealed class StructuralIntegrityPolicyTests
{
    private readonly StructuralIntegrityPolicy _sut = new();

    private static Column Col(string name) => new Column(new SqlIdentifier(name), SqlType.BigInt);

    private static Database Db(params Table[] tables) =>
        new Database([new Schema(new SqlIdentifier("public"), tables: [.. tables])]);

    [Fact]
    public void NoDiagnostics_ForAConsistentSchema()
    {
        // Arrange
        var users = new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pk"), [new SqlIdentifier("id")]), columns: [Col("id")]);
        var orders = new Table(
            new SqlIdentifier("orders"),
            primaryKey: new PrimaryKey(new SqlIdentifier("orders_pk"), [new SqlIdentifier("id")]),
            columns: [Col("id"), Col("user_id")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("orders_users_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("public"), new SqlIdentifier("users"), [new SqlIdentifier("id")])]);

        // Act
        var diagnostics = _sut.Validate(Db(users, orders)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenIndexNameIsReusedAcrossTablesInASchema()
    {
        // Arrange — index names are schema-scoped, so two tables can't both declare 'ix_updated_at'.
        var invoices = new Table(new SqlIdentifier("invoices"), columns: [Col("updated_at")],
            indexes: [new TableIndex(new SqlIdentifier("ix_updated_at"), [new IndexColumn(new SqlIdentifier("updated_at"))])]);
        var orders = new Table(new SqlIdentifier("orders"), columns: [Col("updated_at")],
            indexes: [new TableIndex(new SqlIdentifier("ix_updated_at"), [new IndexColumn(new SqlIdentifier("updated_at"))])]);

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
        var invoices = new Table(new SqlIdentifier("invoices"), primaryKey: new PrimaryKey(new SqlIdentifier("shared_name"), [new SqlIdentifier("id")]), columns: [Col("id")]);
        var orders = new Table(new SqlIdentifier("orders"), columns: [Col("id")],
            indexes: [new TableIndex(new SqlIdentifier("shared_name"), [new IndexColumn(new SqlIdentifier("id"))])]);

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldContain("shared_name");
    }

    [Fact]
    public void NoDiagnostics_WhenTheSameIndexNameIsUsedInDifferentSchemas()
    {
        // Arrange
        Table Invoices() => new(new SqlIdentifier("invoices"), columns: [Col("updated_at")],
            indexes: [new TableIndex(new SqlIdentifier("ix_updated_at"), [new IndexColumn(new SqlIdentifier("updated_at"))])]);
        var schema = new Database(
        [
            new Schema(new SqlIdentifier("billing"), tables: [Invoices()]),
            new Schema(new SqlIdentifier("ordering"), tables: [Invoices()]),
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
        var diagnostics = _sut.Validate(Db(new Table(new SqlIdentifier("empty")))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("no columns"));
    }

    [Fact]
    public void Error_WhenColumnDeclaredTwice()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table(new SqlIdentifier("t"), columns: [Col("id"), Col("ID")]))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void Error_WhenPrimaryKeyReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table(new SqlIdentifier("t"), primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("missing")]), columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Primary key") && d.Message.Contains("missing"));
    }

    [Fact]
    public void Error_WhenIndexReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table(new SqlIdentifier("t"), columns: [Col("id")], indexes: [new TableIndex(new SqlIdentifier("ix"), ["nope"])]);

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
            new SqlIdentifier("t"), columns: [Col("id")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("ghost")], new SqlIdentifier("public"), new SqlIdentifier("t"), [new SqlIdentifier("id")])]);

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
            new SqlIdentifier("t"),
            primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]),
            columns: [Col("id"), Col("a"), Col("b")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("a"), new SqlIdentifier("b")], new SqlIdentifier("public"), new SqlIdentifier("t"), [new SqlIdentifier("id")])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("counts must match"));
    }

    [Fact]
    public void Warning_WhenForeignKeyTargetTableIsUndeclared()
    {
        // Arrange — the target may exist unmanaged (gradual adoption), so the finding advises rather than blocks.
        var table = new Table(
            new SqlIdentifier("t"), columns: [Col("id"), Col("ref")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("ref")], new SqlIdentifier("public"), new SqlIdentifier("absent"), [new SqlIdentifier("id")])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        var finding = diagnostics.ShouldHaveSingleItem();
        finding.Severity.ShouldBe(DiagnosticSeverity.Warning);
        finding.Message.ShouldContain("references table 'public.absent', which this project does not declare");
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetSchemaIsUnmanaged()
    {
        // Arrange — "external" schema is not present in the document at all.
        var table = new Table(
            new SqlIdentifier("t"), columns: [Col("id"), Col("ref")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("ref")], new SqlIdentifier("external"), new SqlIdentifier("other"), [new SqlIdentifier("id")])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesNonUniqueColumns()
    {
        // Arrange — target column exists but is neither a PK nor a unique index.
        var target = new Table(new SqlIdentifier("target"), primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]), columns: [Col("id"), Col("code")]);
        var source = new Table(
            new SqlIdentifier("source"), columns: [Col("id"), Col("code")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("code")], new SqlIdentifier("public"), new SqlIdentifier("target"), [new SqlIdentifier("code")])]);

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
            new SqlIdentifier("target"),
            primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]),
            columns: [Col("id"), Col("code")],
            indexes: [new TableIndex(new SqlIdentifier("uq"), ["code"], isUnique: true)]);
        var source = new Table(
            new SqlIdentifier("source"), columns: [Col("id"), Col("code")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("code")], new SqlIdentifier("public"), new SqlIdentifier("target"), [new SqlIdentifier("code")])]);

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
            new SqlIdentifier("target"),
            primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]),
            columns: [Col("id"), Col("code")],
            indexes: [new TableIndex(new SqlIdentifier("uq"), ["code"], isUnique: true, predicate: new SqlText("code IS NOT NULL"))]);
        var source = new Table(
            new SqlIdentifier("source"), columns: [Col("id"), Col("code")],
            foreignKeys: [new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("code")], new SqlIdentifier("public"), new SqlIdentifier("target"), [new SqlIdentifier("code")])]);

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
        var schema = new Database([
            new Schema(new SqlIdentifier("public"), routines:
            [
                new Routine(new SqlIdentifier("r"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 1 $$")),
                new Routine(new SqlIdentifier("r"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 1 $$")),
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
        var schema = new Database([
            new Schema(new SqlIdentifier("public"), routines:
            [
                new Routine(new SqlIdentifier("f"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 1 $$")),
                new Routine(new SqlIdentifier("f"), RoutineKind.Function, new SqlText("a int"), new SqlText("RETURNS int AS $$ SELECT 2 $$")),
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
        var schema = new Database([
            new Schema(new SqlIdentifier("public"),
                tables: [new Table(new SqlIdentifier("foo"), columns: [Col("id")])],
                views: [new View(new SqlIdentifier("foo"), new SqlText("SELECT 1"))]),
        ]);

        var diagnostics = _sut.Validate(schema).ToList();

        diagnostics.ShouldContain(d => d.Message.Contains("reuses the name 'foo'") && d.Message.Contains("table") && d.Message.Contains("view"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossTableAndEnum()
    {
        // Relations and types share pg_type (a relation has a row type), so a table and an enum collide too.
        var schema = new Database([
            new Schema(new SqlIdentifier("public"),
                tables: [new Table(new SqlIdentifier("status"), columns: [Col("id")])],
                enums: [new EnumType(new SqlIdentifier("status"), ["a", "b"])]),
        ]);

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("reuses the name 'status'"));
    }

    [Fact]
    public void Error_WhenSequenceDeclaredTwice()
    {
        var schema = new Database([
            new Schema(new SqlIdentifier("public"), sequences: [new Sequence(new SqlIdentifier("seq")), new Sequence(new SqlIdentifier("seq"))]),
        ]);

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("declares sequence 'seq' more than once"));
    }

    [Fact]
    public void NoDiagnostics_WhenNamesDifferAcrossKinds()
    {
        var schema = new Database([
            new Schema(new SqlIdentifier("public"),
                tables: [new Table(new SqlIdentifier("t"), columns: [Col("id")])],
                views: [new View(new SqlIdentifier("v"), new SqlText("SELECT 1"))],
                sequences: [new Sequence(new SqlIdentifier("s"))],
                compositeTypes: [new CompositeType(new SqlIdentifier("c"), [new CompositeField(new SqlIdentifier("f"), SqlType.Int)])],
                enums: [new EnumType(new SqlIdentifier("e"), ["a"])],
                domains: [new DomainType(new SqlIdentifier("d"), SqlType.Text)]),
        ]);

        _sut.Validate(schema).ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenColumnHasBothDefaultAndGenerated()
    {
        var table = new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("area"), SqlType.Int, defaultExpression: new SqlText("0"), generatedExpression: new SqlText("w * h"))]);

        _sut.Validate(Db(table)).ShouldContain(d =>
            d.Message.Contains("both a DEFAULT and a GENERATED") && d.Message.Contains("area"));
    }

    [Fact]
    public void NoDiagnostics_ForGeneratedColumnWithoutDefault()
    {
        var table = new Table(new SqlIdentifier("t"),
            primaryKey: new PrimaryKey(new SqlIdentifier("t_pk"), [new SqlIdentifier("id")]),
            columns: [Col("id"), new Column(new SqlIdentifier("area"), SqlType.Int, generatedExpression: new SqlText("w * h"))]);

        _sut.Validate(Db(table)).ShouldBeEmpty();
    }
}
