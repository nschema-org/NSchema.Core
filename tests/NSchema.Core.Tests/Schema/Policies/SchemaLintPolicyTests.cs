using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Policies;

namespace NSchema.Tests.Schema.Policies;

public sealed class SchemaLintPolicyTests
{
    private readonly SchemaLintPolicy _sut = new();

    private static Column Col(string name, bool nullable = false) => Column.Create(name, SqlType.BigInt, isNullable: nullable);

    private static DatabaseSchema Db(params Table[] tables) =>
        DatabaseSchema.Create([SchemaDefinition.Create("public", tables: tables)]);

    [Fact]
    public void NoDiagnostics_ForATableWithANonNullablePrimaryKey()
    {
        // Arrange
        var table = Table.Create("users", primaryKey: new PrimaryKey("pk", ["id"]), columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Warns_WhenTableHasNoPrimaryKey()
    {
        // Act
        var diagnostics = _sut.Validate(Db(Table.Create("events", columns: [Col("id")]))).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(PolicyDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("no primary key");
    }

    [Fact]
    public void Warns_WhenPrimaryKeyColumnIsNullable()
    {
        // Arrange
        var table = Table.Create("t", primaryKey: new PrimaryKey("pk", ["id"]), columns: [Col("id", nullable: true)]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == PolicyDiagnosticSeverity.Warning && d.Message.Contains("forced NOT NULL"));
    }

    [Fact]
    public void Warns_WhenIndexListsAColumnTwice()
    {
        // Arrange
        var table = Table.Create(
            "t",
            primaryKey: new PrimaryKey("pk", ["id"]),
            columns: [Col("id"), Col("a")],
            indexes: [TableIndex.Create("ix", ["a", "a"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == PolicyDiagnosticSeverity.Warning && d.Message.Contains("lists column 'a' more than once"));
    }
}
