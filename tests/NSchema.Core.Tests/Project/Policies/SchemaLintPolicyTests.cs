using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Policies;

namespace NSchema.Tests.Project.Policies;

public sealed class SchemaLintPolicyTests
{
    private readonly SchemaLintPolicy _sut = new();

    private static Column Col(string name, bool nullable = false) => new Column(new SqlIdentifier(name), SqlType.BigInt, IsNullable: nullable);

    private static Database Db(params Table[] tables) =>
        new Database([new Schema(new SqlIdentifier("public"), Tables: tables)]);

    [Fact]
    public void NoDiagnostics_ForATableWithANonNullablePrimaryKey()
    {
        // Arrange
        var table = new Table(new SqlIdentifier("users"), PrimaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]), Columns: [Col("id")]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Warns_WhenTableHasNoPrimaryKey()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table(new SqlIdentifier("events"), Columns: [Col("id")]))).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("no primary key");
    }

    [Fact]
    public void Warns_WhenPrimaryKeyColumnIsNullable()
    {
        // Arrange
        var table = new Table(new SqlIdentifier("t"), PrimaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]), Columns: [Col("id", nullable: true)]);

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
            PrimaryKey: new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("id")]),
            Columns: [Col("id"), Col("a")],
            Indexes: [new TableIndex(new SqlIdentifier("ix"), ["a", "a"])]);

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("lists column 'a' more than once"));
    }
}
