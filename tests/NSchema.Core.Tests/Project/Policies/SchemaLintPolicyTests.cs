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

    private static Column Col(string name, bool nullable = false) => new Column { Name = new SqlIdentifier(name), Type = SqlType.BigInt, IsNullable = nullable };

    private static Database Db(params Table[] tables) =>
        new Database { Schemas = [new Schema { Name = new SqlIdentifier("public"), Tables = [.. tables] }] };

    [Fact]
    public void NoDiagnostics_ForATableWithANonNullablePrimaryKey()
    {
        // Arrange
        var table = new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [Col("id")] };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Warns_WhenTableHasNoPrimaryKey()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table { Name = new SqlIdentifier("events"), Columns = [Col("id")] })).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("no primary key");
    }

    [Fact]
    public void Warns_WhenPrimaryKeyColumnIsNullable()
    {
        // Arrange
        var table = new Table { Name = new SqlIdentifier("t"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [Col("id", nullable: true)] };

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
        var table = new Table
        {
            Name = new SqlIdentifier("t"),
            PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] },
            Columns = [Col("id"), Col("a")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("ix"), Columns = ["a", "a"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d =>
            d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("lists column 'a' more than once"));
    }
}
