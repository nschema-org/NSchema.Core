using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Policies;

namespace NSchema.Tests.Project.Policies;

public sealed class SchemaLintPolicyTests
{
    private readonly SchemaLintPolicy _sut = new();

    private static Column Col(string name, bool nullable = false) => new Column(new SqlIdentifier(name), SqlType.BigInt, isNullable: nullable);

    private static Database Db(params Table[] tables) =>
        new Database([new Schema(new SqlIdentifier("public"), tables: [.. tables])]);

    [Fact]
    public void NoDiagnostics_ForATableWithANonNullablePrimaryKey()
    {
        // Arrange
        var table = new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]), columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Warns_WhenTableHasNoPrimaryKey()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table(new SqlIdentifier("events"), columns: [Col("id")]))).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("no primary key");
    }

    [Fact]
    public void Warns_WhenPrimaryKeyColumnIsNullable()
    {
        // Arrange
        var table = new Table(new SqlIdentifier("t"), primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]), columns: [Col("id", nullable: true)]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("forced NOT NULL"));
    }

    [Fact]
    public void Warns_WhenIndexListsAColumnTwice()
    {
        // Arrange
        var table = new Table(
            new SqlIdentifier("t"),
            primaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]),
            columns: [Col("id"), Col("a")],
            indexes: [new TableIndex(new SqlIdentifier("ix"), ["a", "a"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("lists column 'a' more than once"));
    }
}
