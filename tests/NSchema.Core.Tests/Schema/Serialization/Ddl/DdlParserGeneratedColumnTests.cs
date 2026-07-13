using NSchema.Project.Ddl;
using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Parser coverage for stored generated columns: <c>GENERATED ALWAYS AS (expr) STORED</c>.
/// </summary>
public sealed class DdlParserGeneratedColumnTests
{
    private static Column ParseColumn(string body) =>
        new DdlParser($"CREATE TABLE app.t ({body});").Parse().Schema
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Columns.ShouldHaveSingleItem();

    [Fact]
    public void Parse_Generated_CapturesExpression()
        => ParseColumn("full_name text GENERATED ALWAYS AS (first || ' ' || last) STORED")
            .GeneratedExpression.ShouldBe("first || ' ' || last");

    [Fact]
    public void Parse_PlainColumn_HasNoGeneration()
        => ParseColumn("a int").GeneratedExpression.ShouldBeNull();

    [Fact]
    public void Parse_GeneratedNotNull_CapturesBoth()
    {
        var column = ParseColumn("area int NOT NULL GENERATED ALWAYS AS (w * h) STORED");
        column.IsNullable.ShouldBeFalse();
        column.GeneratedExpression.ShouldBe("w * h");
    }

    [Fact]
    public void Parse_Generated_RoundTripsThroughWriter()
    {
        var schema = new DdlParser("CREATE TABLE app.t (w int, h int, area int GENERATED ALWAYS AS (w * h) STORED);").Parse().Schema;
        var column = new DdlParser(DdlWriter.Instance.Write(schema)).Parse().Schema
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Columns.Last();
        column.Name.ShouldBe("area");
        column.GeneratedExpression.ShouldBe("w * h");
    }
}
