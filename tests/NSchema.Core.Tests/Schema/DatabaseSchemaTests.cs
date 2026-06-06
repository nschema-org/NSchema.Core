using NSchema.Schema.Model;

namespace NSchema.Tests.Schema;

public sealed class DatabaseSchemaTests
{
    private static DatabaseSchema Sample() => new(
        [SchemaDefinition.Create("app"), SchemaDefinition.Create("audit"), SchemaDefinition.Create("legacy")],
        ["old", "scratch"]);

    [Fact]
    public void Filter_RestrictsBothSchemasAndDroppedSchemas()
    {
        var result = Sample().Filter(["app", "old"]);

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBe(["old"]);
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        var schema = new DatabaseSchema([SchemaDefinition.Create("App")], ["Old"]);

        var result = schema.Filter(["app", "old"]);

        result.Schemas.Select(s => s.Name).ShouldBe(["App"]);
        result.DroppedSchemas.ShouldBe(["Old"]);
    }

    [Fact]
    public void Filter_NamesNotPresent_AreIgnored()
    {
        var result = Sample().Filter(["app", "does-not-exist"]);

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public void Filter_NullScope_ReturnsEverything()
    {
        var schema = Sample();

        schema.Filter(null).ShouldBe(schema);
    }

    [Fact]
    public void Filter_EmptyScope_ReturnsEverything()
    {
        var schema = Sample();

        schema.Filter([]).ShouldBe(schema);
    }
}
