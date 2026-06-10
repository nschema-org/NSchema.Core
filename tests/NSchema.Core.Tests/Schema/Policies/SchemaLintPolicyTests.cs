using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Policies;

namespace NSchema.Tests.Schema.Policies;

public sealed class SchemaLintPolicyTests
{
    private readonly SchemaLintPolicy _sut = new();

    private static Column Col(string name, bool nullable = false) => new Column(name, SqlType.BigInt, IsNullable: nullable);

    private static DatabaseSchema Db(params Table[] tables) =>
        new DatabaseSchema([new SchemaDefinition("public", Tables: tables)]);

    [Fact]
    public void NoDiagnostics_ForATableWithANonNullablePrimaryKey()
    {
        // Arrange
        var table = new Table("users", PrimaryKey: new PrimaryKey("pk", ["id"]), Columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Warns_WhenTableHasNoPrimaryKey()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table("events", Columns: [Col("id")]))).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(PolicyDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("no primary key");
    }

    [Fact]
    public void Warns_WhenPrimaryKeyColumnIsNullable()
    {
        // Arrange
        var table = new Table("t", PrimaryKey: new PrimaryKey("pk", ["id"]), Columns: [Col("id", nullable: true)]);

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
        var table = new Table(
            "t",
            PrimaryKey: new PrimaryKey("pk", ["id"]),
            Columns: [Col("id"), Col("a")],
            Indexes: [new TableIndex("ix", ["a", "a"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == PolicyDiagnosticSeverity.Warning && d.Message.Contains("lists column 'a' more than once"));
    }
}
