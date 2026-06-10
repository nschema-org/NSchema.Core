using NSchema.Policies;
using NSchema.Schema.Model;
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
    public void Error_WhenTableHasNoColumns()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table("empty"))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Severity == PolicyDiagnosticSeverity.Error && d.Message.Contains("no columns"));
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
            new SchemaDefinition("public",
                Functions: [new Function("r", "", "RETURNS int AS $$ SELECT 1 $$")],
                Procedures: [new Procedure("r", "", "AS $$ SELECT 1 $$")]),
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
            new SchemaDefinition("public", Functions:
            [
                new Function("f", "", "RETURNS int AS $$ SELECT 1 $$"),
                new Function("f", "a int", "RETURNS int AS $$ SELECT 2 $$"),
            ]),
        ]);

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert — overloading is not supported: one routine per name.
        diagnostics.ShouldContain(d => d.Message.Contains("declares function 'f' more than once"));
    }
}
