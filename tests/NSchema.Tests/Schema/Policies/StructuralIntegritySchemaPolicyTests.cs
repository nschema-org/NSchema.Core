using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Policies;

namespace NSchema.Tests.Schema.Policies;

public sealed class StructuralIntegritySchemaPolicyTests
{
    private readonly StructuralIntegritySchemaPolicy _sut = new();

    private static Column Col(string name) => Column.Create(name, SqlType.BigInt);

    private static DatabaseSchema Db(params Table[] tables) =>
        DatabaseSchema.Create([SchemaDefinition.Create("public", tables: tables)]);

    [Fact]
    public void NoDiagnostics_ForAConsistentSchema()
    {
        // Arrange
        var users = Table.Create("users", primaryKey: new PrimaryKey("users_pk", ["id"]), columns: [Col("id")]);
        var orders = Table.Create(
            "orders",
            primaryKey: new PrimaryKey("orders_pk", ["id"]),
            columns: [Col("id"), Col("user_id")],
            foreignKeys: [ForeignKey.Create("orders_users_fk", ["user_id"], "public", "users", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(users, orders)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenTableHasNoColumns()
    {
        // Act
        var diagnostics = _sut.Validate(Db(Table.Create("empty"))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Severity == PolicyDiagnosticSeverity.Error && d.Message.Contains("no columns"));
    }

    [Fact]
    public void Error_WhenColumnDeclaredTwice()
    {
        // Act
        var diagnostics = _sut.Validate(Db(Table.Create("t", columns: [Col("id"), Col("ID")]))).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void Error_WhenPrimaryKeyReferencesUnknownColumn()
    {
        // Arrange
        var table = Table.Create("t", primaryKey: new PrimaryKey("pk", ["missing"]), columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Primary key") && d.Message.Contains("missing"));
    }

    [Fact]
    public void Error_WhenIndexReferencesUnknownColumn()
    {
        // Arrange
        var table = Table.Create("t", columns: [Col("id")], indexes: [TableIndex.Create("ix", ["nope"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Index") && d.Message.Contains("nope"));
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesUnknownLocalColumn()
    {
        // Arrange
        var table = Table.Create(
            "t", columns: [Col("id")],
            foreignKeys: [ForeignKey.Create("fk", ["ghost"], "public", "t", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("unknown local column 'ghost'"));
    }

    [Fact]
    public void Error_WhenForeignKeyArityMismatches()
    {
        // Arrange
        var table = Table.Create(
            "t",
            primaryKey: new PrimaryKey("pk", ["id"]),
            columns: [Col("id"), Col("a"), Col("b")],
            foreignKeys: [ForeignKey.Create("fk", ["a", "b"], "public", "t", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("counts must match"));
    }

    [Fact]
    public void Error_WhenForeignKeyTargetTableMissingInManagedSchema()
    {
        // Arrange
        var table = Table.Create(
            "t", columns: [Col("id"), Col("ref")],
            foreignKeys: [ForeignKey.Create("fk", ["ref"], "public", "absent", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("unknown table 'public.absent'"));
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetMissingInPartialSchema()
    {
        // Arrange
        var table = Table.Create(
            "t", columns: [Col("id"), Col("ref")],
            foreignKeys: [ForeignKey.Create("fk", ["ref"], "public", "absent", ["id"])]);
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("public", isPartial: true, tables: [table])]);

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetSchemaIsUnmanaged()
    {
        // Arrange — "external" schema is not present in the document at all.
        var table = Table.Create(
            "t", columns: [Col("id"), Col("ref")],
            foreignKeys: [ForeignKey.Create("fk", ["ref"], "external", "other", ["id"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesNonUniqueColumns()
    {
        // Arrange — target column exists but is neither a PK nor a unique index.
        var target = Table.Create("target", primaryKey: new PrimaryKey("pk", ["id"]), columns: [Col("id"), Col("code")]);
        var source = Table.Create(
            "source", columns: [Col("id"), Col("code")],
            foreignKeys: [ForeignKey.Create("fk", ["code"], "public", "target", ["code"])]);

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void NoError_WhenForeignKeyReferencesUniqueIndex()
    {
        // Arrange
        var target = Table.Create(
            "target",
            primaryKey: new PrimaryKey("pk", ["id"]),
            columns: [Col("id"), Col("code")],
            indexes: [TableIndex.Create("uq", ["code"], isUnique: true)]);
        var source = Table.Create(
            "source", columns: [Col("id"), Col("code")],
            foreignKeys: [ForeignKey.Create("fk", ["code"], "public", "target", ["code"])]);

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesPredicatedUniqueIndex()
    {
        // Arrange — a partial (predicated) unique index cannot back a foreign key.
        var target = Table.Create(
            "target",
            primaryKey: new PrimaryKey("pk", ["id"]),
            columns: [Col("id"), Col("code")],
            indexes: [TableIndex.Create("uq", ["code"], isUnique: true, predicate: "code IS NOT NULL")]);
        var source = Table.Create(
            "source", columns: [Col("id"), Col("code")],
            foreignKeys: [ForeignKey.Create("fk", ["code"], "public", "target", ["code"])]);

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }
}
