using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class StateBackedSchemaProviderTests
{
    private readonly ISchemaStateStore _store = Substitute.For<ISchemaStateStore>();

    [Fact]
    public async Task GetSchema_NoState_ReturnsEmptySchema()
    {
        // Arrange
        _store.Read(Arg.Any<CancellationToken>()).Returns((DatabaseSchema?)null);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert: an empty schema makes a first-run plan show a full create.
        result.Schemas.ShouldBeEmpty();
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSchema_WithState_ReturnsPersistedSchema()
    {
        // Arrange
        var persisted = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert: an unscoped read returns the persisted schema unchanged (by value).
        result.ShouldBe(persisted);
    }

    [Fact]
    public async Task GetSchema_EmptyScope_ReturnsPersistedSchema()
    {
        // Arrange: an empty scope means "return everything", same as null.
        var persisted = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema([], TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(persisted);
    }

    [Fact]
    public async Task GetSchema_WithScope_FiltersToRequestedSchemas()
    {
        // Arrange: the store snapshots the whole database (e.g. includes the default "public"
        // schema), but a scoped read must only return the managed schemas — otherwise the diff
        // would plan to drop the unmanaged ones.
        var persisted = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("my_schema"),
            SchemaDefinition.Create("public")
        ]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema(["my_schema"], TestContext.Current.CancellationToken);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["my_schema"]);
    }

    [Fact]
    public async Task GetSchema_WithScope_MatchesSchemaNamesCaseInsensitively()
    {
        // Arrange: scope matching mirrors the comparer's OrdinalIgnoreCase comparison.
        var persisted = DatabaseSchema.Create([SchemaDefinition.Create("My_Schema")]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema(["my_schema"], TestContext.Current.CancellationToken);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["My_Schema"]);
    }

}
