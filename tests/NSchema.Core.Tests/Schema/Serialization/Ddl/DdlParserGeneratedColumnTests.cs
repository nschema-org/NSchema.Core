using NSchema.Project.Ddl;
using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Parser coverage for stored generated columns: <c>GENERATED ALWAYS AS (expr) STORED</c>.
/// </summary>
public sealed class DdlParserGeneratedColumnTests
{
    private static Column ParseColumn(string body) =>
        new TestDdlParser($"CREATE TABLE app.t ({body});").Parse().Schema
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Columns.ShouldHaveSingleItem();

    [Fact]
    public void Parse_Generated_CapturesExpression()
        => ShouldlyIdentifierExtensions.ShouldBe(ParseColumn("full_name text GENERATED ALWAYS AS (first || ' ' || last) STORED")
                .GeneratedExpression, "first || ' ' || last");

    [Fact]
    public void Parse_PlainColumn_HasNoGeneration()
        => ParseColumn("a int").GeneratedExpression.ShouldBeNull();

    [Fact]
    public void Parse_GeneratedNotNull_CapturesBoth()
    {
        var column = ParseColumn("area int NOT NULL GENERATED ALWAYS AS (w * h) STORED");
        column.IsNullable.ShouldBeFalse();
        ShouldlyIdentifierExtensions.ShouldBe(column.GeneratedExpression, "w * h");
    }

    [Fact]
    public void Parse_Generated_RoundTripsThroughWriter()
    {
        var schema = new TestDdlParser("CREATE TABLE app.t (w int, h int, area int GENERATED ALWAYS AS (w * h) STORED);").Parse().Schema;
        var column = new TestDdlParser(DdlWriter.Instance.Write(schema)).Parse().Schema
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Columns.Last();
        ShouldlyIdentifierExtensions.ShouldBe(column.Name, "area");
        ShouldlyIdentifierExtensions.ShouldBe(column.GeneratedExpression, "w * h");
    }
}
