using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Current;
using NSchema.Current.Storage;
using NSchema.Current.Storage.Backends;
using NSchema.Current.Domain.Models;

namespace NSchema.Tests.State;

public sealed class CurrentSchemaProviderOfflineReadTests
{
    private readonly ISchemaStateStore _store = Substitute.For<ISchemaStateStore>();
    private readonly ISchemaStateSerializer _serializer = new SchemaStateSerializer();

    private CurrentSchemaProvider BuildSut() => new(_serializer, online: null, store: _store);

    private void Persisted(DatabaseSchema schema) =>
        _store.Read(Arg.Any<CancellationToken>()).Returns(_serializer.Serialize(new SchemaState(schema)));

    [Fact]
    public async Task GetSchema_NoState_ReturnsEmptySchema()
    {
        // Arrange
        _store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)null);
        var sut = BuildSut();

        // Act
        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: an empty schema makes a first-run plan show a full create.
        result.Schemas.ShouldBeEmpty();
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSchema_WithState_ReturnsPersistedSchema()
    {
        // Arrange
        Persisted(new DatabaseSchema([new SchemaDefinition("app")]));
        var sut = BuildSut();

        // Act
        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: an unscoped read returns the persisted schema.
        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public async Task GetSchema_EmptyScope_ReturnsPersistedSchema()
    {
        // Arrange: an empty scope means "return everything", same as null.
        Persisted(new DatabaseSchema([new SchemaDefinition("app")]));
        var sut = BuildSut();

        // Act
        var result = await sut.GetSchema(SchemaSourceMode.Offline, [], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public async Task GetSchema_WithScope_FiltersToRequestedSchemas()
    {
        // Arrange: the store snapshots the whole database (e.g. includes the default "public"
        // schema), but a scoped read must only return the managed schemas — otherwise the diff
        // would plan to drop the unmanaged ones.
        Persisted(new DatabaseSchema(
        [
            new SchemaDefinition("my_schema"),
            new SchemaDefinition("public")
        ]));
        var sut = BuildSut();

        // Act
        var result = await sut.GetSchema(SchemaSourceMode.Offline, ["my_schema"], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["my_schema"]);
    }

    [Fact]
    public async Task GetSchema_WithScope_MatchesSchemaNamesCaseInsensitively()
    {
        // Arrange: scope matching mirrors the comparer's OrdinalIgnoreCase comparison.
        Persisted(new DatabaseSchema([new SchemaDefinition("My_Schema")]));
        var sut = BuildSut();

        // Act
        var result = await sut.GetSchema(SchemaSourceMode.Offline, ["my_schema"], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["My_Schema"]);
    }
}
